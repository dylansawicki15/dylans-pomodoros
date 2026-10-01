# The only way this project should reach Fly: tests first, deploy only if they pass.
# A bare `dotnet test` at the repo root runs zero tests and still exits 0, so the test project is named explicitly.
$repoRoot = Join-Path $PSScriptRoot ".."

dotnet test (Join-Path $repoRoot "tests/PersonalLifeApi.Tests")

if ($LASTEXITCODE -ne 0) { throw "The tests failed so we arent going to deploy. (exit code $LASTEXITCODE)" }

fly deploy $repoRoot
