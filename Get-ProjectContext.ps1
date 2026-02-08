# Run this from your WinUI 3 Project root directory.

$outputFile = "ProjectContext_Dump.txt"

# WinUI 3 / .NET Development Extensions
$allowedExtensions = @(".cs", ".xaml", ".csproj", ".json", ".md", ".manifest", ".appxmanifest", ".config")

# Standard .NET / WinUI Ignore list
$ignoredFolders = @("bin", "obj", ".git", ".vs", "PublishProfiles", "Assets", "TestResults")

# Priority files to appear first (Architecture & Configuration)
$priorityFiles = @("PROJECT_STATE.md", "Preferences.md", "appsettings.json")

# Clear existing dump
if (Test-Path -LiteralPath $outputFile) { Remove-Item -LiteralPath $outputFile }

$sb = [System.Text.StringBuilder]::new()

# 1. Write Timestamp Header
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
[void]$sb.AppendLine("// ---------------------------------------------------------")
[void]$sb.AppendLine("// GENERATED: $timestamp")
[void]$sb.AppendLine("// PROJECT: NavigationIntegrationSystem (WinUI 3 / C#)")
[void]$sb.AppendLine("// ---------------------------------------------------------`n")

# 2. Process Priority Files First
foreach ($pFileName in $priorityFiles) {
    $foundFile = Get-ChildItem -Recurse -Filter $pFileName | Select-Object -First 1
    if ($foundFile) {
        $relativePath = Resolve-Path -Path $foundFile.FullName -Relative
        
        [void]$sb.AppendLine("`n// ---------------------------------------------------------")
        [void]$sb.AppendLine("// FILE: $relativePath")
        [void]$sb.AppendLine("// ---------------------------------------------------------`n")
        
        $content = Get-Content -LiteralPath $foundFile.FullName -Raw
        [void]$sb.AppendLine($content)
    }
}

# 3. Get all other files recursively
$files = Get-ChildItem -Recurse | Where-Object {
    $ext = $_.Extension
    $path = $_.FullName
    $name = $_.Name
    
    # Skip directories
    if ($_.PSIsContainer) { return $false }

    # Skip priority files (already added)
    if ($priorityFiles -contains $name) { return $false }

    # Filter by extension
    if ($allowedExtensions -notcontains $ext) { return $false }

    # Filter by ignored folders
    foreach ($ignore in $ignoredFolders) {
        if ($path -match "[\\/]$ignore([\\/]|$)") { return $false }
    }
    
    # Exclude designer/generated files often found in WinUI
    if ($name -match "\.g\.cs$" -or $name -match "\.i\.cs$") { return $false }

    return $true
}

# 4. Process Remaining Files
foreach ($file in $files) {
    $relativePath = Resolve-Path -Path $file.FullName -Relative
    
    [void]$sb.AppendLine("`n// ---------------------------------------------------------")
    [void]$sb.AppendLine("// FILE: $relativePath")
    [void]$sb.AppendLine("// ---------------------------------------------------------`n")
    
    $content = Get-Content -LiteralPath $file.FullName -Raw
    [void]$sb.AppendLine($content)
}

# Write to file and clipboard
$finalString = $sb.ToString()
$finalString | Set-Content -LiteralPath $outputFile -Encoding UTF8
$finalString | Set-Clipboard

Write-Host "Success! Scanned $( ($files | Measure-Object).Count + $priorityFiles.Count ) files."
Write-Host "Context saved to '$outputFile' and copied to clipboard."