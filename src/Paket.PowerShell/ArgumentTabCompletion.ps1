
# http://stackoverflow.com/questions/30923696/add-custom-argument-completer-for-cmdlet

# https://github.com/mariuszwojcik/RabbitMQTools/blob/master/TabExpansions.ps1
function createCompletionResult([string]$text, [string]$value, [string]$tooltip) {
    if ([string]::IsNullOrEmpty($value)) { return }
    if ([string]::IsNullOrEmpty($text)) { $text = $value }
    if ([string]::IsNullOrEmpty($tooltip)) { $tooltip = $value }
    $completionText = @{$true="'$value'"; $false=$value  }[$value -match "\W"]
    $completionText = $completionText -replace '\[', '``[' -replace '\]', '``]'
    New-Object System.Management.Automation.CompletionResult $completionText, $text, 'ParameterValue', $tooltip | write
}

$findPackages = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    Paket-FindPackages -SearchText $wordToComplete -Max 100 | % {
        createCompletionResult $_ $_ $_ | write
    }
}

$findPackageVersions = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
    if (-not $fakeBoundParameter.NuGet){ return }
    Paket-FindPackageVersions -Name $fakeBoundParameter.NuGet -Max 100 | % {
        createCompletionResult $_ $_ $_ | write
    }
}

# create and add $global:options to the list of completers
# http://www.powertheshell.com/dynamicargumentcompletion/
if (-not $global:options) { $global:options = @{CustomArgumentCompleters = @{};NativeArgumentCompleters = @{}}}

$global:options['CustomArgumentCompleters']['Paket-Add:NuGet'] = $findPackages
$global:options['CustomArgumentCompleters']['Paket-Add:Version'] = $findPackageVersions

$function:tabexpansion2 = $function:tabexpansion2 -replace 'End\r\n{','End { if ($null -ne $options) { $options += $global:options} else {$options = $global:options}'