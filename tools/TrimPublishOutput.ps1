param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDir
)

$ErrorActionPreference = 'Stop'
$PublishDir = $PublishDir.Trim('"', "'")
$resolvedPublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$allowedCultures = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$allowedCultures.Add('en-US') | Out-Null
$allowedCultures.Add('zh-CN') | Out-Null

Get-ChildItem -LiteralPath $resolvedPublishDir -Directory |
    Where-Object { $_.Name -like '*-*' -and -not $allowedCultures.Contains($_.Name) } |
    Remove-Item -Recurse -Force

$redundantFilePatterns = @(
    'DirectML.dll',
    'Microsoft.ML.OnnxRuntime.dll',
    'onnxruntime.dll',
    'Microsoft.Graphics.Imaging*',
    'Microsoft.Graphics.Internal.Imaging*',
    'Microsoft.Windows.AI.*',
    'Microsoft.Windows.ImageCreationInternal*',
    'Microsoft.Windows.Internal.AI*',
    'Microsoft.Windows.Internal.ImageCreation*',
    'Microsoft.Windows.Internal.SemanticSearch*',
    'Microsoft.Windows.Internal.Vision*',
    'Microsoft.Windows.Private.Workloads*',
    'Microsoft.Windows.SemanticSearch*',
    'Microsoft.Windows.Vision*',
    'Microsoft.Windows.Workloads*',
    'Microsoft.Windows.Widgets*',
    'NPUDetect.dll',
    'PerceptiveStreaming.dll',
    'System.Numerics.Tensors.dll',
    'workloads*.json'
)

foreach ($pattern in $redundantFilePatterns) {
    Get-ChildItem -LiteralPath $resolvedPublishDir -File -Filter $pattern |
        Remove-Item -Force
}
