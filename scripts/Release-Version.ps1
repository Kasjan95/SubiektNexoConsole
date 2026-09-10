# Shared version selection helpers. Dot-sourcing this file has no side effects.
function Find-PublishGit {
    $command = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($root in @($env:ProgramW6432, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        foreach ($relative in @('Git\cmd\git.exe', 'Programs\Git\cmd\git.exe')) {
            $candidate = Join-Path $root $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Get-PublishSourceRevision {
    param([Parameter(Mandatory)][string] $RepositoryRoot, [string] $GitPath, [switch] $Production)
    $revision = $null
    $status = @()
    if ($GitPath) {
        try {
            $result = @(& $GitPath -C $RepositoryRoot rev-parse HEAD 2>$null)
            $revisionExit = $LASTEXITCODE
            $status = @(& $GitPath -C $RepositoryRoot status --porcelain --untracked-files=normal 2>$null)
            $statusExit = $LASTEXITCODE
            if ($revisionExit -eq 0 -and $statusExit -eq 0 -and $result.Count -eq 1 -and $result[0] -match '^[a-fA-F0-9]{40,64}$') {
                $revision = $result[0].Trim()
            }
        } catch { $revision = $null }
    }
    if (!$revision) {
        if ($Production) { throw 'Production wymaga dostepnego Git i poprawnego repozytorium. Zainstaluj Git for Windows lub dodaj git.exe do PATH. Nie zmieniono wersji na tym etapie.' }
        Write-Warning 'Brak dostepnych metadanych Git. Publish testowy bedzie kontynuowany z identyfikatorem nogit (bez informacji o commicie).'
        return ('nogit.' + [Guid]::NewGuid().ToString('N'))
    }
    if ($status.Count -gt 0) {
        if ($Production) { throw 'Production wymaga czystego repozytorium. Zacommituj Release.props i pozostale zmiany, potem uruchom z -VersionLevel Current.' }
        $revision += '.dirty'
    }
    return $revision
}

function ConvertTo-ReleaseVersion {
    param([Parameter(Mandatory)][string] $Value)
    if ($Value -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Wersja musi miec 3 lub 4 czlony liczbowe.' }
    $parts = @($Value.Split('.') | ForEach-Object { [int]$_ })
    if ($parts.Count -eq 3) { $parts += 0 }
    if (@($parts | Where-Object { $_ -gt 65534 }).Count -gt 0) { throw 'Kazdy czlon wersji musi miescic sie w zakresie 0-65534 (wersja assembly .NET).' }
    return ($parts -join '.')
}

function Get-NextReleaseVersion {
    param([Parameter(Mandatory)][string] $Current,
        [Parameter(Mandatory)][ValidateSet('Major', 'Minor', 'Patch', 'Revision', 'Current')][string] $Level)
    $normalized = ConvertTo-ReleaseVersion $Current
    if ($Level -eq 'Current') { return $normalized }
    $index = @{ Major = 0; Minor = 1; Patch = 2; Revision = 3 }[$Level]
    $parts = @($normalized.Split('.') | ForEach-Object { [int]$_ })
    if ($parts[$index] -eq 65534) { throw 'Ten poziom osiagnal limit 65534. Wybierz wyzszy poziom aktualizacji.' }
    $parts[$index]++
    for ($i = $index + 1; $i -lt 4; $i++) { $parts[$i] = 0 }
    return ($parts -join '.')
}

function Select-ReleaseVersion {
    param([Parameter(Mandatory)][string] $Current,
        [ValidateSet('Major', 'Minor', 'Patch', 'Revision', 'Current')][string] $Level,
        [switch] $Yes)
    $Current = ConvertTo-ReleaseVersion $Current
    if ([string]::IsNullOrEmpty($Level)) {
        Write-Host "Obecna wersja: $Current"
        $levels = @('Major', 'Minor', 'Patch', 'Revision', 'Current')
        $labels = @('Duza wersja', 'Nowe funkcje', 'Poprawki', 'Drobna rewizja', 'Uzyj obecnej (np. ponowienie nieudanego publishu)')
        for ($i = 0; $i -lt 5; $i++) {
            try { $next = Get-NextReleaseVersion $Current $levels[$i] } catch { $next = 'limit - wybierz wyzszy poziom' }
            Write-Host ("{0}. {1}: {2}" -f ($i + 1), $labels[$i], $next)
        }
        do {
            $choice = Read-Host 'Wybierz 1-5 lub Q, aby anulowac'
            if ($null -eq $choice -or $choice -match '^[qQ]$') { throw [OperationCanceledException]::new('Anulowano publish. Wersja nie zostala zmieniona.') }
        } until ($choice -match '^[1-5]$')
        $Level = $levels[[int]$choice - 1]
    }
    $target = Get-NextReleaseVersion $Current $Level
    Write-Host "Wersja wydania: $Current -> $target"
    if (!$Yes -and $target -ne $Current) {
        $confirmation = Read-Host 'Zapisac nowa wersje i kontynuowac publish? [T/N]'
        if ($confirmation -notmatch '^[tTyY]$') { throw [OperationCanceledException]::new('Anulowano publish. Wersja nie zostala zmieniona.') }
    }
    return $target
}

function Set-ReleaseVersion {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Version)
    $normalized = ConvertTo-ReleaseVersion $Version
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($Path)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/ReleaseVersion')
    if ($null -eq $node) { throw 'Brak ReleaseVersion w Release.props.' }
    if ($node.InnerText -eq $normalized) { return }
    $node.InnerText = $normalized
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $backup = $temporary + '.bak'
    try {
        $xml.Save($temporary)
        [System.IO.File]::Replace($temporary, $Path, $backup)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
        if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup }
    }
}
