$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "live-imap-result-validation.ps1")

$passed = 0

function Assert-ImapValidation {
    param(
        [string]$Name,
        [object]$Actual,
        [bool]$ExpectedExactSequence,
        [string]$ExpectedShape,
        [int]$ExpectedCount
    )

    if ($Actual.exactSequence -ne $ExpectedExactSequence) {
        throw "${Name}: expected exactSequence=$ExpectedExactSequence, got $($Actual.exactSequence)."
    }
    if ($Actual.shape -ne $ExpectedShape) {
        throw "${Name}: expected shape=$ExpectedShape, got $($Actual.shape)."
    }
    if ($Actual.count -ne $ExpectedCount) {
        throw "${Name}: expected count=$ExpectedCount, got $($Actual.count)."
    }
    $script:passed++
}

$fixture = 1..1000
$searchLine = "* SEARCH " + ($fixture -join " ")
$sortLine = "* SORT " + ($fixture -join " ")

$validSearch = Test-ImapResultSequence -Lines @($searchLine) -Command SEARCH -ExpectedCount 1000
Assert-ImapValidation "valid SEARCH" $validSearch $true "sequence" 1000
if ($validSearch.first -ne 1 -or $validSearch.last -ne 1000) {
    throw "valid SEARCH: expected first=1 and last=1000."
}

$validSort = Test-ImapResultSequence -Lines @($sortLine) -Command SORT -ExpectedCount 1000
Assert-ImapValidation "valid SORT" $validSort $true "sequence" 1000

$wrongOrderFixture = $fixture.Clone()
$wrongOrderFixture[99] = 101
$wrongOrderFixture[100] = 100
$wrongOrder = Test-ImapResultSequence -Lines @("* SEARCH " + ($wrongOrderFixture -join " ")) -Command SEARCH -ExpectedCount 1000
Assert-ImapValidation "wrong order" $wrongOrder $false "sequence" 1000

$wrongTokenFixture = $fixture.Clone()
$wrongTokenFixture[49] = "not-a-number"
$wrongToken = Test-ImapResultSequence -Lines @("* SORT " + ($wrongTokenFixture -join " ")) -Command SORT -ExpectedCount 1000
Assert-ImapValidation "wrong token" $wrongToken $false "malformed" 0

$zeroSearch = Test-ImapResultSequence -Lines @("* SEARCH") -Command SEARCH -ExpectedCount 0
Assert-ImapValidation "zero SEARCH" $zeroSearch $true "zero" 0
$zeroSort = Test-ImapResultSequence -Lines @("* SORT") -Command SORT -ExpectedCount 0
Assert-ImapValidation "zero SORT" $zeroSort $true "zero" 0

$trailingZero = Test-ImapResultSequence -Lines @("* SEARCH ") -Command SEARCH -ExpectedCount 0
Assert-ImapValidation "zero trailing whitespace" $trailingZero $false "malformed" 0

$malformed = Test-ImapResultSequence -Lines @("* SEARCH 1 nope") -Command SEARCH -ExpectedCount 1000
Assert-ImapValidation "malformed line" $malformed $false "malformed" 0

$missing = Test-ImapResultSequence -Lines @("* EXISTS 1") -Command SEARCH -ExpectedCount 1000
Assert-ImapValidation "missing line" $missing $false "missing" 0

Write-Output "PASS: live IMAP result validation tests ($passed assertions)"
