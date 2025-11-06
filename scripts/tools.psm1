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

.OUTPUTS

System.String. Add-Extension returns a string with the extension
or file name.
#>
function Add-ExtensionV2 {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter()]
        [string] $Extension = "txt"
    )

    $Name, $Extension -join '='
}


<#
.SYNOPSIS

Calculate the sum of 2 integers.

.DESCRIPTION

Calculate the sum of 2 integers.

.PARAMETER number1
The first integer.

.PARAMETER number2
The second integer.

.OUTPUTS

System.Int32. Add-Number returns a number that is the sum.
#>
function Add-Number {
    param(
        [Parameter(Mandatory)]
        [int] $number1,

        [Parameter(Mandatory)]
        [int] $number2
    )

    $number1 + $number2
}
