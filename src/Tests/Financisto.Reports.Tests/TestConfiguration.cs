using Xunit;

// The reports read process-wide state (the lookup lists of DbManual, the culture of LocalizationService) and a few
// tests change it, so all tests share one collection and run one after another.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
