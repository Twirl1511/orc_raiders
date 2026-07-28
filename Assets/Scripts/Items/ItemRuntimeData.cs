public sealed class ItemRuntimeData
{
    public ItemRuntimeData(int instanceId, ItemDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
    }

    public int InstanceId { get; }
    public ItemDefinition Definition { get; }

    public string Id => Definition != null ? Definition.Id : "";
    public string DisplayName => Definition != null ? Definition.DisplayName : "";
}
