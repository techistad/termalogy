param(
    [Parameter(Mandatory = $true)]
    [string]$Owner,

    [Parameter(Mandatory = $true)]
    [string]$Repo
)

$body = @{
    required_status_checks         = @{
        strict   = $true
        contexts = @("branch-policy", "build-windows")
    }
    enforce_admins                 = $true
    required_pull_request_reviews  = @{
        dismiss_stale_reviews           = $true
        require_code_owner_reviews      = $false
        required_approving_review_count = 1
    }
    restrictions                   = $null
    required_linear_history        = $true
    required_conversation_resolution = $true
    allow_force_pushes             = $false
    allow_deletions                = $false
}

$json = $body | ConvertTo-Json -Depth 10

Write-Host "Applying branch protection to $Owner/$Repo (main)..."
$json | gh api --method PUT `
    -H "Accept: application/vnd.github+json" `
    -H "X-GitHub-Api-Version: 2022-11-28" `
    "repos/$Owner/$Repo/branches/main/protection" `
    --input -
Write-Host "Branch protection applied."
