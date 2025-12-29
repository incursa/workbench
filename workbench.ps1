param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

dotnet run --project $PSScriptRoot/src/Workbench/Workbench.csproj -- @Args
