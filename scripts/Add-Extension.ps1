<#
.SYNOPSIS

Adds a file name extension to a supplied name.

.DESCRIPTION

Adds a file name extension to a supplied name.
Takes any strings for the file name or extension.

.PARAMETER Name
The file name to add extension to.

.PARAMETER Extension
The file extension to use.

.PARAMETER UseGroup
Indicates whether group the result in curly brace.

.OUTPUTS

System.String. Add-Extension returns a string with the extension
or file name.
#>
param(
    [Parameter(Mandatory)]
    [string] $Name,

    [string] $Extension = "txt",
    [switch] $UseGroup
)

$result = $Name, $Extension -join '.'

$UseGroup ? "{$result}" : $result
