<#
.SYNOPSIS

Adds a file name extension to a supplied name.

.DESCRIPTION

Adds a file name extension to a supplied name.
Takes any strings for the file name or extension.

.PARAMETER Name
First operand of the addition.

.PARAMETER Extension
Second operand of the addition.

.OUTPUTS

System.String. Add-Extension returns a string with the extension
or file name.
#>
param(
    [Parameter(Mandatory)]
    [string] $Name,

    [Parameter()]
    [string] $Extension = "txt"
)

$Name, $Extension -join '.'
