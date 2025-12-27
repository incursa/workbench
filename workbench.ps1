param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

dotnet run --project src/Workbench/Workbench.csproj -- @Args
