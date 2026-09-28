# Registers a Windows scheduled task that pushes YAPA pomodoros to the live API.
# No secret in here: push-pomodoros.ps1 reads the key from the User env var PERSONAL_LIFE_API_KEY.
$pusher = Join-Path $PSScriptRoot 'push-pomodoros.ps1'

$action = New-ScheduledTaskAction -Execute "$env:LOCALAPPDATA\Microsoft\WindowsApps\pwsh.exe" `
    -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -File `"$pusher`" -ApiUrl https://personal-life-api.fly.dev"

$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 240)

# If the PC was asleep or off at a scheduled time, run as soon as it's back.
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

Register-ScheduledTask -TaskName 'PersonalLifeApi-PushPomodoros' -Action $action -Trigger $trigger -Settings $settings -Force
