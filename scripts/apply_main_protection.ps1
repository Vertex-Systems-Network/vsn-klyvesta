[CmdletBinding()]
param(
    [string]$Repository = "Vertex-Systems-Network/vsn-klyvesta",
    [string]$Branch = "main",
    [Parameter(Mandatory = $true)]
    [ValidateSet("independent", "solo-self-review")]
    [string]$ReviewMode
)

$ErrorActionPreference = "Stop"
$GitHubActionsAppId = 15368
$ApiVersion = "2026-03-10"
$RequiredChecks = @(
    "Build and architecture verify",
    "Analyze C#",
    "PostgreSQL migration and constraints",
    "Repository governance"
)

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI is required to apply repository protection."
}

gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated."
}

$requiredApprovals = if ($ReviewMode -eq "independent") { 1 } else { 0 }
$requireLastPushApproval = $ReviewMode -eq "independent"
$checks = @($RequiredChecks | ForEach-Object {
    [ordered]@{ context = $_; app_id = $GitHubActionsAppId }
})

$payload = [ordered]@{
    required_status_checks = [ordered]@{
        strict = $true
        contexts = @()
        checks = $checks
    }
    enforce_admins = $true
    required_pull_request_reviews = [ordered]@{
        dismiss_stale_reviews = $true
        require_code_owner_reviews = $false
        required_approving_review_count = $requiredApprovals
        require_last_push_approval = $requireLastPushApproval
        dismissal_restrictions = @{}
        bypass_pull_request_allowances = @{}
    }
    restrictions = $null
    required_conversation_resolution = $true
    allow_force_pushes = $false
    allow_deletions = $false
    block_creations = $false
    lock_branch = $false
    allow_fork_syncing = $false
}

$temp = New-TemporaryFile
try {
    $payload | ConvertTo-Json -Depth 10 | Set-Content -Path $temp -Encoding utf8
    gh api `
        --method PUT `
        -H "Accept: application/vnd.github+json" `
        -H "X-GitHub-Api-Version: $ApiVersion" `
        "repos/$Repository/branches/$Branch/protection" `
        --input $temp
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub rejected the branch-protection update."
    }
}
finally {
    Remove-Item $temp -Force -ErrorAction SilentlyContinue
}

Write-Host "Applied protected-$Branch policy to $Repository."
Write-Host "Required GitHub Actions checks: $($RequiredChecks -join ', ')"
if ($ReviewMode -eq "independent") {
    Write-Host "Review mode: at least one independent approval; stale approvals dismissed; last-push approval required."
}
else {
    Write-Warning "Review mode: explicit solo SELF REVIEW exception. Independent approval is not claimed."
}
Write-Host "Run scripts/verify_main_protection.ps1 with the same ReviewMode before closing ABD-44."
