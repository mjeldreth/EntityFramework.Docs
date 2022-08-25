using NewInEfCore7;

public class Program
{
    public static async Task Main()
    {
        // await TpcInheritanceSample.Inheritance_with_TPH();
        // await TpcInheritanceSample.Inheritance_with_TPT();
        // await TpcInheritanceSample.Inheritance_with_TPC();
        // await TpcInheritanceSample.Inheritance_with_TPC_using_HiLo();
        // await TpcInheritanceSample.Inheritance_with_TPC_using_Identity();
        //
        // await ExecuteDeleteSample.ExecuteDelete();
        // await ExecuteDeleteSample.ExecuteDeleteTpt();
        // await ExecuteDeleteSample.ExecuteDeleteTpc();
        // await ExecuteDeleteSample.ExecuteDeleteSqlite();
        //
        // await ExecuteUpdateSample.ExecuteUpdate();
        // await ExecuteUpdateSample.ExecuteUpdateTpt();
        // await ExecuteUpdateSample.ExecuteUpdateTpc();
        // await ExecuteUpdateSample.ExecuteUpdateSqlite();

        await ModelBuildingConventionsSample.No_foreign_key_index_convention();
        // await ModelBuildingConventionsSample.No_cascade_delete_convention();
        // await ModelBuildingConventionsSample.Map_members_explicitly_by_attribute_convention();
    }
}
