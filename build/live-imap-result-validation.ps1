function Test-ImapResultSequence {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string[]]$Lines,
        [ValidateSet("SEARCH", "SORT")]
        [string]$Command,
        [ValidateRange(0, 1000000)]
        [int]$ExpectedCount = 1000
    )

    $candidate = $null
    foreach ($line in @($Lines)) {
        if ($null -eq $line) {
            continue
        }

        $match = [regex]::Match([string]$line, '^\* (SEARCH|SORT)(.*)$')
        if ($match.Success -and $match.Groups[1].Value -ceq $Command) {
            $candidate = [string]$line
        }
    }

    $result = [ordered]@{
        found = $false
        command = $null
        count = 0
        first = $null
        last = $null
        exactSequence = $false
        shape = "missing"
        error = $null
    }

    if ($null -eq $candidate) {
        $result.error = "No untagged * $Command result line was found."
        return [pscustomobject]$result
    }

    $result.found = $true
    $result.command = $Command
    $lineMatch = [regex]::Match($candidate, '^\* (SEARCH|SORT)(.*)$')
    $remainder = $lineMatch.Groups[2].Value

    if ($remainder.Length -eq 0) {
        $result.shape = "zero"
        $result.exactSequence = ($ExpectedCount -eq 0)
        if (-not $result.exactSequence) {
            $result.error = "Expected $ExpectedCount result values, got zero."
        }
        return [pscustomobject]$result
    }

    if (-not $remainder.StartsWith(" ", [StringComparison]::Ordinal)) {
        $result.shape = "malformed"
        $result.error = "Result values must follow the command name with one space."
        return [pscustomobject]$result
    }

    $payload = $remainder.Substring(1)
    if ($payload.Length -eq 0 -or $payload.StartsWith(" ", [StringComparison]::Ordinal) -or $payload.EndsWith(" ", [StringComparison]::Ordinal)) {
        $result.shape = "malformed"
        $result.error = "A zero-result line must have no trailing space and nonzero values must use single spaces."
        return [pscustomobject]$result
    }

    $values = [System.Collections.Generic.List[long]]::new()
    foreach ($token in $payload.Split(" ", [StringSplitOptions]::None)) {
        if ($token -notmatch '^\d+$') {
            $result.shape = "malformed"
            $result.error = "Result contains a nonnumeric token."
            return [pscustomobject]$result
        }

        [long]$parsed = 0
        if (-not [long]::TryParse(
                $token,
                [Globalization.NumberStyles]::None,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsed)) {
            $result.shape = "malformed"
            $result.error = "Result contains a numeric token outside the supported range."
            return [pscustomobject]$result
        }
        $values.Add($parsed)
    }

    $result.shape = "sequence"
    $result.count = $values.Count
    $result.first = $values[0]
    $result.last = $values[$values.Count - 1]
    $result.exactSequence = ($values.Count -eq $ExpectedCount)
    if ($result.exactSequence) {
        for ($index = 0; $index -lt $values.Count; $index++) {
            if ($values[$index] -ne ($index + 1)) {
                $result.exactSequence = $false
                break
            }
        }
    }

    if (-not $result.exactSequence) {
        if ($values.Count -ne $ExpectedCount) {
            $result.error = "Expected $ExpectedCount result values, got $($values.Count)."
        }
        else {
            $result.error = "Result values are not in exact 1..$ExpectedCount order."
        }
    }

    return [pscustomobject]$result
}
