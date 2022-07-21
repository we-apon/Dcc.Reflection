using Dcc.Extensions;
using MassTransit;
using MassTransit.Testing;

namespace Dcc.SpecFlow.MassTransit;

public static class StateMachineTestHarnessExtensions {
    public static async Task<ISagaInstance<TState>?> AwaitInstanceInState<TMachine, TState>(this ISagaStateMachineTestHarness<TMachine, TState> saga, Guid correlationId, string stateName, TimeSpan? timeout = null)
        where TState : class, SagaStateMachineInstance
        where TMachine : SagaStateMachine<TState> {

        var elements = await saga.Exists(state => state.CorrelationId == correlationId, machine => machine.GetPropertyValue<State>(stateName), timeout ?? TimeSpan.FromSeconds(5));
        if (elements?.Any() != true)
            return null;

        return await saga.Sagas.SelectAsync(x => x.CorrelationId == elements.First()).First();
    }

    public static async Task<ISagaInstance<TState>?> GetInstance<TMachine, TState>(this ISagaStateMachineTestHarness<TMachine, TState> saga, Guid correlationId)
        where TState : class, SagaStateMachineInstance
        where TMachine : SagaStateMachine<TState> {

        return await saga.Sagas.SelectAsync(x => x.CorrelationId == correlationId).FirstOrDefault();
    }
}
