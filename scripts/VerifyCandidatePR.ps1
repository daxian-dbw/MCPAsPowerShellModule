<#
.SYNOPSIS

Verifies if a specific pull request from the PowerShell repository is eligible for backport to a target release.

.DESCRIPTION

This script validates whether a specific pull request is ready for backporting to a specified target release version. It performs several validation checks:

1. Verifies the PR is in a merged state
2. Checks if the PR has already been backported (has "Backport-{TargetRelease}.x-Done" label)
3. Searches for existing backport PRs to prevent duplicates

The script uses the GitHub CLI to fetch PR information and returns a validation result object with detailed feedback about the backport eligibility.

.PARAMETER PRNumber
The pull request number to verify for backport eligibility. This parameter is mandatory.

.PARAMETER TargetRelease
The target PowerShell release version to check for backport eligibility. Must be one of: '7.4', '7.5', or '7.6'.
This parameter is mandatory and determines which backport version to validate against.

.OUTPUTS

System.Management.Automation.PSCustomObject. Returns a PowerShell object containing:
- pr_info: Original PR information (state, merge_commit, title, author, labels)
- validation_status: "success" if eligible for backport, "failed" if not
- validation_feedback: Detailed message explaining the validation result
- existing_backport_pr_info: Information about existing backport PR (if found)
- original_pr_Info: Original PR info when backport already exists
#>

param(
    [parameter(Mandatory)]
    [string] $PRNumber,

    [ValidateSet('7.4', '7.5', '7.6')]
    [parameter(Mandatory)]
    [string] $TargetRelease
)

$prJson = gh pr view  --repo PowerShell/PowerShell `
  --json state,mergeCommit,title,author,labels `
  --jq '{state: .state, merge_commit: .mergeCommit.oid, title: .title, author: .author.login, labels: [.labels[].name]}'

$prInfo = $prJson | ConvertFrom-Json

if ($prInfo.state -ne "MERGED") {
    return [PSCustomObject]@{
        pr_info = $prInfo
        validation_status = "failed"
        validation_feedback = "The PR #$PRNumber hasn't been merged (state: $($prInfo.state)). Please stop the backport and inform the user."
    }
}

if ($prInfo.labels -contains "Backport-$TargetRelease.x-Done") {
    return [PSCustomObject]@{
        pr_info = $prInfo
        validation_status = "failed"
        validation_feedback = "The PR has the label 'Backport-$TargetRelease.x-Done', which indicates it was already backported successfully. Please stop the backport and inform the user."
    }
}

$existingBackportPR = gh pr list --repo PowerShell/PowerShell `
    --search "in:title [release/v$TargetRelease] $($prInfo.title)" `
    --state all `
    --json number,state | ConvertFrom-Json

if ($existingBackportPR) {
    return [PSCustomObject]@{
        original_pr_Info = $prInfo
        existing_backport_pr_info = $existingBackportPR
        validation_status = "failed"
        validation_feedback = "The backport PR for the original PR #$PRNumber already exists: #$($existingBackportPR.number). Ask the user if they want to continue the backport."
    }
}

return [PSCustomObject]@{
    pr_info = $prInfo
    validation_status = "success"
    validation_feedback = "The PR was merged and hasn't been backported yet. Please proceed."
}
