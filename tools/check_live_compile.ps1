param([string]$Editor = "C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor")
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $response = Get-ChildItem Library/Bee/artifacts -Filter umamusume.rsp -Recurse |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$response) { throw "Open the project once in Unity to generate the compiler response file." }
    $lines = Get-Content $response.FullName
    $known = @{}
    foreach ($line in $lines) { $known[$line.Trim('"').Replace('\', '/')] = $true }
    $extra = Get-ChildItem Assets/Scripts/umamusume -Recurse -Filter *.cs | ForEach-Object {
        $relative = $_.FullName.Substring($root.Length+1).Replace('\', '/')
        if (!$known.ContainsKey($relative)) {
            # Respect nested Unity assembly boundaries (including disabled legacy code).
            $directory = $_.Directory
            $nestedAssembly = $false
            while ($directory.FullName -ne (Join-Path $root "Assets/Scripts/umamusume")) {
                if (@(Get-ChildItem -LiteralPath $directory.FullName -Filter *.asmdef).Count -gt 0 -or
                    @(Get-ChildItem -LiteralPath $directory.FullName -Filter *.asmref).Count -gt 0) {
                    $nestedAssembly = $true
                    break
                }
                $directory = $directory.Parent
                if ($null -eq $directory) { throw "Source outside assembly root: $relative" }
            }
            if (!$nestedAssembly) { $relative }
        }
    }
    $output = Join-Path $root "tmp/live-validation-compile"
    New-Item -ItemType Directory -Force $output | Out-Null
    & "$Editor/Data/NetCoreRuntime/dotnet.exe" exec "$Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig "@$($response.FullName)" $extra "/out:$output/umamusume.dll" "/refout:$output/umamusume.ref.dll" 2>&1 |
        Tee-Object "$output/compile.log"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $output/compile.log" }
} finally { Pop-Location }

