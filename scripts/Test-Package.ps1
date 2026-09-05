param([Parameter(Mandatory)][string]$Archive)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @('LuckyTouchInput.exe', 'README.md', 'README.zh-CN.md', 'LICENSE',
        'docs/PRIVACY.md', 'docs/ARCHITECTURE.md', 'assets/icon.ico')) {
        if ($entries -notcontains $required) { throw "Missing package entry: $required" }
    }
    if ($entries | Where-Object { $_ -match '(^|/)(\.git|settings.ini|native-keyboard-lease.ini)(/|$)|\.pdb$|(^|/)\.\.' }) {
        throw 'Package contains development state or an unsafe path.'
    }
    foreach ($entry in $zip.Entries | Where-Object { $_.FullName -match '\.md$' }) {
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $markdown = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $links = @([regex]::Matches($markdown, '\]\(([^)]+)\)') | ForEach-Object { $_.Groups[1].Value })
        $links += @([regex]::Matches($markdown, 'src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
        foreach ($link in $links) {
            if ($link -match '^[a-zA-Z]+:|^#') { continue }
            $relative = $link.Split('#')[0]
            $base = 'https://package.invalid/' + $entry.FullName.Replace('\', '/')
            $resolved = [uri]::new([uri]$base, $relative).AbsolutePath.TrimStart('/')
            if ($entries -notcontains $resolved) { throw "Broken packaged link in $($entry.FullName): $link" }
        }
    }
    Write-Output "Package checks passed: $($zip.Entries.Count) entries; local documentation links resolve."
}
finally { $zip.Dispose() }
