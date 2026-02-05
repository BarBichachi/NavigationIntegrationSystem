# Get-ProjectContext.ps1
# Run this from your Solution root directory.

$outputFile = "ProjectContext_Dump.txt"
# Allowed extensions
$allowedExtensions = @(".cs", ".xaml", ".json", ".csproj", ".appxmanifest", ".md")
$ignoredFolders = @("bin", "obj", ".vs", ".git", "Debug", "Release", "Assets", "Properties")

# Priority files to appear first (order matters)
$priorityFiles = @("Preferences.md", "PROJECT_STATE.md")

# Clear existing dump
if (Test-Path $outputFile) { Remove-Item $outputFile }

$sb = [System.Text.StringBuilder]::new()

# 1. Write Timestamp Header
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
[void]$sb.AppendLine("// ---------------------------------------------------------")
[void]$sb.AppendLine("// GENERATED: $timestamp")
[void]$sb.AppendLine("// ---------------------------------------------------------`n")

# 2. Process Priority Files First
foreach ($pFileName in $priorityFiles) {
    if (Test-Path $pFileName) {
        $relativePath = ".\$pFileName"
        
        # Append Header
        [void]$sb.AppendLine("`n// ---------------------------------------------------------")
        [void]$sb.AppendLine("// FILE: $relativePath")
        [void]$sb.AppendLine("// ---------------------------------------------------------`n")
        
        # Append Content
        $content = Get-Content $pFileName -Raw
        [void]$sb.AppendLine($content)
    }
}

# 3. Get all other files recursively
$files = Get-ChildItem -Recurse | Where-Object {
    $ext = $_.Extension
    $path = $_.FullName
    $name = $_.Name
    
    # Skip priority files (already added)
    if ($priorityFiles -contains $name) { return $false }

    # Filter by extension
    if ($allowedExtensions -notcontains $ext) { return $false }

    # Filter by ignored folders
    foreach ($ignore in $ignoredFolders) {
        if ($path -match "\\$ignore\\") { return $false }
    }

    return $true
}

# 4. Process Remaining Files
foreach ($file in $files) {
    # Calculate relative path for cleaner context
    $relativePath = $file.FullName.Replace($PWD.Path, "")
    
    # Append Header
    [void]$sb.AppendLine("`n// ---------------------------------------------------------")
    [void]$sb.AppendLine("// FILE: $relativePath")
    [void]$sb.AppendLine("// ---------------------------------------------------------`n")
    
    # Append Content
    $content = Get-Content $file.FullName -Raw
    [void]$sb.AppendLine($content)
}

# Write to file and clipboard
$sb.ToString() | Set-Content $outputFile -Encoding UTF8
$sb.ToString() | Set-Clipboard

Write-Host "Success! Scanned $( $files.Count + $priorityFiles.Count ) files."
Write-Host "Context saved to '$outputFile' and copied to clipboard."