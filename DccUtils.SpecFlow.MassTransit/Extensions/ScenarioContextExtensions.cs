using MassTransit;
using MassTransit.Testing;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow.MassTransit;

public static class ScenarioContextExtensions {
    public static ISagaStateMachineTestHarness<TStateMachine, TInstance> GetSaga<TStateMachine, TInstance>(this ScenarioContext context) where TStateMachine : SagaStateMachine<TInstance> where TInstance : class, SagaStateMachineInstance {
        return context.Get<ISagaStateMachineTestHarness<TStateMachine, TInstance>>();
    }
}
