namespace TechStore.E2E;

[CollectionDefinition(Nombre)]
public class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Nombre = "E2E";
}
