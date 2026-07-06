$run = Invoke-RestMethod -Uri 'https://api.github.com/repos/sadsafadadg/ASFASGF/actions/runs/28778749505' -Headers @{'Accept'='application/vnd.github+json'}
$jobs = Invoke-RestMethod -Uri $run.jobs_url -Headers @{'Accept'='application/vnd.github+json'}
foreach ($job in $jobs.jobs) {
    foreach ($step in $job.steps) {
        if ($step.conclusion -eq 'failure') {
            Write-Output "=== Failed: $($step.name) ==="
        }
    }
    $ann = Invoke-RestMethod -Uri "https://api.github.com/repos/sadsafadadg/ASFASGF/check-runs/$($job.id)/annotations" -Headers @{'Accept'='application/vnd.github+json'}
    foreach ($a in $ann) {
        Write-Output "  $($a.message)"
    }
}
