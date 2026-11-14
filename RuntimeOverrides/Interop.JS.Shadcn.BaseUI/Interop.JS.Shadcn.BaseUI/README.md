# Interop.JS.Shadcn.BaseUI

Selected managed bindings for the upstream `@base-ui/react` package boundary used
by the pinned FECSN/shadcn profile. This is not a general Base UI adapter.
The public `BaseUI` façade exposes the selected primitives as nested FECSX
components such as `BaseUI.Button`, `BaseUI.Progress.Root`, and
`BaseUI.Dialog.Popup`, plus `BaseUI.Toast.Add`. Opaque React handles and raw import
signatures remain internal.
