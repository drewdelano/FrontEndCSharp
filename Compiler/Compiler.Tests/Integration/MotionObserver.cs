using System.Text.Json;

namespace FrontEndCSharp.Compiler.Integration;

internal sealed class MotionObservationResult
{
    public required double ReadyMilliseconds { get; init; }
    public required string Dom { get; init; }
    public required string JsxTextParity { get; init; }
    public required MotionEvidence Evidence { get; init; }
    public required MotionVerification Verification { get; init; }
    public required FormValidationObservation FormValidation { get; init; }
    public required FecscnObservation Fecscn { get; init; }
}

internal sealed class FecscnObservation
{
    public required string CardBorderRadius { get; init; }
    public required string InputHeight { get; init; }
    public required string ButtonDisplay { get; init; }
    public required string ButtonHeight { get; init; }
}

internal sealed class FormValidationObservation
{
    public required string InvalidFocusIntentError { get; init; }
    public required string InvalidAccountabilityEmailError { get; init; }
    public required string InvalidPriorityError { get; init; }
    public required string OutOfRangePriorityValue { get; init; }
    public required string ValidResult { get; init; }
}

internal static class MotionObserver
{
    private const string Recorder = """
        (() => {
            const evidence = globalThis.__fecsMotionEvidence = {
                samples: [],
                phase: "entrance",
                prefersReducedMotion: matchMedia("(prefers-reduced-motion: reduce)").matches
            };
            const started = performance.now();
            function value(selector) {
                const node = document.querySelector(selector);
                if (!node) return null;
                const style = getComputedStyle(node);
                return {
                    time: performance.now(),
                    opacity: Number(style.opacity),
                    y: style.transform === "none" ? 0 : new DOMMatrixReadOnly(style.transform).m42
                };
            }
            function record() {
                if (evidence.samples.length >= 2000) return;
                const focus = value(".focus-card");
                const task = value(".task-card");
                if (focus || task) evidence.samples.push({ phase: evidence.phase, focus, task });
                const orbit = document.querySelector(".orbit-visual.is-running");
                if (orbit) {
                    const style = getComputedStyle(orbit);
                    evidence.cssOrbitAnimation = {
                        name: style.animationName,
                        duration: style.animationDuration,
                        iterationCount: style.animationIterationCount
                    };
                }
            }
            const observer = new MutationObserver(record);
            observer.observe(document, { childList: true, subtree: true });
            function frame() {
                record();
                if (performance.now() - started < 15000) requestAnimationFrame(frame);
                else observer.disconnect();
            }
            requestAnimationFrame(frame);
        })();
        """;

    public static async Task<MotionObservationResult> ObserveAsync(
        string browserPath,
        string profilePath,
        string url)
    {
        var readyStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var browser = await ChromiumCdpSession.LaunchAsync(
            browserPath,
            profilePath);
        await browser.SendAsync(
            "Emulation.setDeviceMetricsOverride",
            new { width = 1280, height = 1000, deviceScaleFactor = 1, mobile = false });
        await browser.SendAsync(
            "Page.addScriptToEvaluateOnNewDocument",
            new { source = Recorder });
        await browser.SendAsync("Page.navigate", new { url });
        await browser.WaitForAsync(
            """
            document.body?.getAttribute('data-orbit-status') === 'ready'
                && document.querySelector('.weather-panel.is-ready, .weather-panel.is-failed')
                && globalThis.__fecsMotionEvidence?.samples.some(
                    s => s.focus?.opacity > 0.999 && Math.abs(s.focus.y) < 0.05)
            """,
            "The settled entrance");
        readyStopwatch.Stop();
        var position = await browser.EvaluateAsync("""
            (() => {
                const task = document.querySelector('.task-card');
                task.scrollIntoView({ block: 'center' });
                const rect = task.getBoundingClientRect();
                globalThis.__fecsMotionEvidence.phase = 'hover';
                return { x: rect.x + rect.width / 2, y: rect.y + rect.height / 2 };
            })()
            """);
        await browser.SendAsync(
            "Input.dispatchMouseEvent",
            new
            {
                type = "mouseMoved",
                x = position.GetProperty("x").GetDouble(),
                y = position.GetProperty("y").GetDouble(),
            });
        await browser.WaitForAsync(
            """
            globalThis.__fecsMotionEvidence.samples.some(
                s => s.phase === 'hover' && s.task && Math.abs(s.task.y + 4) < 0.1)
            """,
            "The settled task-card hover");
        await browser.EvaluateAsync(
            "document.querySelector('.intent-submit').click()");
        await browser.WaitForAsync(
            """
            document.querySelector('.intent-error-focus')?.textContent === 'Name one focus outcome.'
                && document.querySelector('.intent-error-email')?.textContent === 'Add an accountability email.'
                && document.querySelector('.intent-error-priority')?.textContent === 'Choose a priority from 1 to 3.'
            """,
            "The managed invalid-form result");
        var invalidFocusIntentError = (await browser.EvaluateAsync(
            "document.querySelector('.intent-error-focus').textContent")).GetString()
            ?? string.Empty;
        var invalidAccountabilityEmailError = (await browser.EvaluateAsync(
            "document.querySelector('.intent-error-email').textContent")).GetString()
            ?? string.Empty;
        var invalidPriorityError = (await browser.EvaluateAsync(
            "document.querySelector('.intent-error-priority').textContent")).GetString()
            ?? string.Empty;
        await browser.EvaluateAsync(
            """
            (() => {
                const input = document.querySelector('#Priority');
                const valueSetter = Object.getOwnPropertyDescriptor(
                    HTMLInputElement.prototype,
                    'value').set;
                valueSetter.call(input, '5');
                input.dispatchEvent(new Event('input', { bubbles: true }));
            })()
            """);
        await browser.WaitForAsync(
            """
            document.querySelector('#Priority')?.value === '5'
                && document.querySelector('.intent-error-priority')?.textContent === ''
            """,
            "The out-of-range priority input update");
        await browser.EvaluateAsync(
            "document.querySelector('.intent-submit').click()");
        await browser.WaitForAsync(
            """
            document.querySelector('#Priority')?.value === '5'
                && document.querySelector('.intent-error-priority')?.textContent === 'Choose a priority from 1 to 3.'
            """,
            "The retained out-of-range priority result");
        var outOfRangePriorityValue = (await browser.EvaluateAsync(
            "document.querySelector('#Priority').value")).GetString()
            ?? string.Empty;
        await browser.EvaluateAsync(
            """
            (() => {
                const values = [
                    ['#FocusIntent', 'Ship React validation'],
                    ['#AccountabilityEmail', 'teammate@example.com'],
                    ['#Priority', '2']
                ];
                const valueSetter = Object.getOwnPropertyDescriptor(
                    HTMLInputElement.prototype,
                    'value').set;
                for (const [selector, value] of values) {
                    const input = document.querySelector(selector);
                    valueSetter.call(input, value);
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                }
            })()
            """);
        await browser.WaitForAsync(
            """
            document.querySelector('#FocusIntent')?.value === 'Ship React validation'
                && document.querySelector('#AccountabilityEmail')?.value === 'teammate@example.com'
                && document.querySelector('#Priority')?.value === '2'
                && document.querySelector('.intent-error-focus')?.textContent === ''
                && document.querySelector('.intent-error-email')?.textContent === ''
                && document.querySelector('.intent-error-priority')?.textContent === ''
            """,
            "The managed form input update");
        await browser.EvaluateAsync(
            "document.querySelector('.intent-submit').click()");
        await browser.WaitForAsync(
            """
            document.querySelector('.intent-success')?.textContent
                === 'Intent saved: Ship React validation'
            """,
            "The managed valid-form result");
        var validResult = (await browser.EvaluateAsync(
            "document.querySelector('.intent-success').textContent")).GetString()
            ?? string.Empty;
        var fecscnJson = await browser.EvaluateAsync(
            """
            (() => {
                const card = document.querySelector('.intent-panel');
                const input = document.querySelector('[data-slot="input"]');
                const button = document.querySelector('[data-slot="button"]');
                const cardStyle = getComputedStyle(card);
                const inputStyle = getComputedStyle(input);
                const buttonStyle = getComputedStyle(button);
                return {
                    CardBorderRadius: cardStyle.borderTopLeftRadius,
                    InputHeight: inputStyle.height,
                    ButtonDisplay: buttonStyle.display,
                    ButtonHeight: buttonStyle.height
                };
            })()
            """);
        var fecscn = fecscnJson.Deserialize<FecscnObservation>()
            ?? throw new InvalidDataException("The browser returned no fecscn style evidence.");
        var evidenceJson = await browser.EvaluateAsync("globalThis.__fecsMotionEvidence");
        var evidence = evidenceJson.Deserialize<MotionEvidence>()
            ?? throw new InvalidDataException("The browser returned no motion evidence.");
        var dom = (await browser.EvaluateAsync("document.documentElement.outerHTML")).GetString()
            ?? string.Empty;
        var jsxTextParity = (await browser.EvaluateAsync(
            "document.querySelector('#fecsx-jsx-text-parity')?.textContent")).GetString()
            ?? string.Empty;
        return new MotionObservationResult
        {
            ReadyMilliseconds = readyStopwatch.Elapsed.TotalMilliseconds,
            Dom = dom,
            JsxTextParity = jsxTextParity,
            Evidence = evidence,
            Verification = MotionEvidenceVerifier.Verify(evidence),
            Fecscn = fecscn,
            FormValidation = new FormValidationObservation
            {
                InvalidFocusIntentError = invalidFocusIntentError,
                InvalidAccountabilityEmailError = invalidAccountabilityEmailError,
                InvalidPriorityError = invalidPriorityError,
                OutOfRangePriorityValue = outOfRangePriorityValue,
                ValidResult = validResult,
            },
        };
    }
}
