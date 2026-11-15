@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "RepositoryRoot=%~dp0"
set "DryRun="
set "Failed="

if /i "%~1"=="/dry-run" (
    set "DryRun=1"
) else if not "%~1"=="" (
    echo Usage: %~nx0 [/dry-run]
    exit /b 2
)

if not exist "%RepositoryRoot%.git" (
    echo Refusing to clean: "%RepositoryRoot%" is not a Git repository root.
    exit /b 1
)

if not exist "%RepositoryRoot%Directory.Build.props" (
    echo Refusing to clean: Directory.Build.props was not found in "%RepositoryRoot%".
    exit /b 1
)

if defined DryRun (
    echo Directories that would be deleted beneath "%RepositoryRoot%":
) else (
    echo Deleting bin and obj directories beneath "%RepositoryRoot%"...
)

for /d /r "%RepositoryRoot%" %%D in (bin obj) do (
    if exist "%%~fD\" (
        if defined DryRun (
            echo "%%~fD"
        ) else (
            echo Deleting "%%~fD"
            rd /s /q "%%~fD"
            if exist "%%~fD\" (
                echo Failed to delete "%%~fD".
                set "Failed=1"
            )
        )
    )
)

if defined Failed (
    echo One or more directories could not be deleted.
    exit /b 1
)

if defined DryRun (
    echo Dry run complete. Nothing was deleted.
) else (
    echo Cleanup complete.
)

exit /b 0
