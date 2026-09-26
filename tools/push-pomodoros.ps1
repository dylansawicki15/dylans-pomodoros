param(
    [string]$YapaDb = "$env:USERPROFILE\Documents\YAPA2\Yapa.db",
    [string]$ApiUrl = "http://localhost:5023",
    [string]$ApiKey = $env:PERSONAL_LIFE_API_KEY 
)

if (([string]::IsNullOrWhiteSpace($apiKey))) { throw "env var PERSONAL_LIFE_API_KEY is empty. please set. or else. im in your walls." }
$json = sqlite3 -json $YapaDb "SELECT Id, DurationMin, ProfileName, DateTime FROM Pomodoros;"
if ($LASTEXITCODE -ne 0) { throw "sqlite3 could not read '$YapaDb' (exit code $LASTEXITCODE)" }
$rows = $json | Out-String | ConvertFrom-Json

$pomodoros = foreach ($row in $rows) {
    @{
        id          = $row.Id
        profileName = $row.ProfileName
        duration    = $row.DurationMin
        endTime     = [DateTimeOffset]::Parse($row.DateTime, [cultureinfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal).ToString("o")
    }
}

$body = $pomodoros | ConvertTo-Json -AsArray
Invoke-RestMethod -Uri "$ApiUrl/pomodoros" -Method Post -Body $body -ContentType "application/json" -Headers @{ "X-API-Key" = $ApiKey } 