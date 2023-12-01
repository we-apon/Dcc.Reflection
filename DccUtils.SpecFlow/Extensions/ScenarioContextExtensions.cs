using TechTalk.SpecFlow;

namespace Dcc.SpecFlow;

public static class ScenarioContextExtensions {
    public static T GetOrSet<T>(this ScenarioContext context, T value) {
        if (context.TryGetValue(out T existed)) {
            return existed;
        }

        context.Set(value);
        return value;
    }

    public static bool PrintResponses(this ScenarioContext context)
        => context.ScenarioInfo.ScenarioAndFeatureTags.Contains("PrintResponses");

    public static bool AttachResponses(this ScenarioContext context)
        => context.ScenarioInfo.ScenarioAndFeatureTags.Contains("AttachResponses");

}
