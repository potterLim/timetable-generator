using System;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceConcurrencyException : Exception
{
    public PlanningWorkspaceConcurrencyToken ExpectedToken { get; }

    public PlanningWorkspaceConcurrencyToken ActualToken { get; }

    public PlanningWorkspaceConcurrencyException(PlanningWorkspaceConcurrencyToken expectedToken, PlanningWorkspaceConcurrencyToken actualToken)
        : base("The planning workspace changed after it was loaded.")
    {
        ExpectedToken = expectedToken;
        ActualToken = actualToken;
    }
}
