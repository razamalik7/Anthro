param(
    [Parameter(Position = 0)]
    [ValidateSet("push", "pull", "status")]
    [string]$Action = "push",

    [string]$ConversationId = "558ef6c7-9dbb-45a2-b218-4b7cd16add51",
    [string]$PrivateRepoUrl = "https://github.com/razamalik7/anthro-brain-private.git"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SyncDir = Join-Path $RepoRoot "runs\private-brain"
$LocalBrainRoot = Join-Path $env:USERPROFILE ".gemini\antigravity\brain"

function Ensure-PrivateRepo {
    if (-not (Test-Path (Join-Path $SyncDir ".git"))) {
        Write-Host "Cloning private brain repository into $SyncDir ..." -ForegroundColor Cyan
        if (Test-Path $SyncDir) {
            Remove-Item -Recurse -Force $SyncDir
        }
        git clone $PrivateRepoUrl $SyncDir
    }
}

if ($Action -eq "status") {
    Ensure-PrivateRepo
    Push-Location $SyncDir
    try {
        git status
    } finally {
        Pop-Location
    }
    exit 0
}

if ($Action -eq "push") {
    Ensure-PrivateRepo

    $SourceConvDir = Join-Path $LocalBrainRoot $ConversationId
    if (-not (Test-Path $SourceConvDir)) {
        Write-Error "Conversation directory not found: $SourceConvDir"
    }

    $DestConvDir = Join-Path $SyncDir "brain\$ConversationId"
    New-Item -ItemType Directory -Force -Path $DestConvDir | Out-Null

    Write-Host "Syncing conversation $ConversationId to private staging folder..." -ForegroundColor Cyan
    robocopy $SourceConvDir $DestConvDir /MIR /XD "tempmediaStorage" /XF "*.tmp" "*.lock" /NFL /NDL /NJH /NJS | Out-Null

    # Also find any subagent conversation IDs referenced in transcript.jsonl and sync them
    $TranscriptPath = Join-Path $SourceConvDir ".system_generated\logs\transcript.jsonl"
    if (Test-Path $TranscriptPath) {
        $SubMatches = Select-String -Path $TranscriptPath -Pattern '"conversation_id"\s*:\s*"([0-9a-f\-]{36})"' -AllMatches
        $SubIds = @()
        foreach ($m in $SubMatches.Matches) {
            $id = $m.Groups[1].Value
            if ($id -ne $ConversationId -and ($SubIds -notcontains $id)) {
                $SubIds += $id
            }
        }
        foreach ($subId in $SubIds) {
            $subSrc = Join-Path $LocalBrainRoot $subId
            if (Test-Path $subSrc) {
                $subDst = Join-Path $SyncDir "brain\$subId"
                Write-Host "Syncing subagent brain $subId ..." -ForegroundColor DarkCyan
                New-Item -ItemType Directory -Force -Path $subDst | Out-Null
                robocopy $subSrc $subDst /MIR /XD "tempmediaStorage" /XF "*.tmp" "*.lock" /NFL /NDL /NJH /NJS | Out-Null
            }
        }
    }

    Push-Location $SyncDir
    try {
        git add -A
        $status = git status --porcelain
        if ($status) {
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            git commit -m "Sync Antigravity brain ($ConversationId) at $timestamp"
            git push origin main
            Write-Host "Successfully pushed private brain context to $PrivateRepoUrl" -ForegroundColor Green
        } else {
            Write-Host "Private brain repo is already up to date." -ForegroundColor Green
        }
    } finally {
        Pop-Location
    }
}

if ($Action -eq "pull") {
    Ensure-PrivateRepo
    Push-Location $SyncDir
    try {
        Write-Host "Pulling latest private brain context from $PrivateRepoUrl ..." -ForegroundColor Cyan
        git pull origin main
    } finally {
        Pop-Location
    }

    $StagedBrainRoot = Join-Path $SyncDir "brain"
    if (Test-Path $StagedBrainRoot) {
        Get-ChildItem -Path $StagedBrainRoot -Directory | ForEach-Object {
            $id = $_.Name
            $dst = Join-Path $LocalBrainRoot $id
            Write-Host "Restoring brain $id into $dst ..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            robocopy $_.FullName $dst /MIR /NFL /NDL /NJH /NJS | Out-Null
        }
        Write-Host "All private brain transcripts & artifacts restored to $LocalBrainRoot" -ForegroundColor Green
    }
}
