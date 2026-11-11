using TFM_ACCOUNTING.Business.Obligations;
using TFM_ACCOUNTING.Business.Attachments;
using Microsoft.Data.SqlClient;
using TFM_ACCOUNTING.Business.OwnCompanies;
using TFM_ACCOUNTING.Business.Partners;
using TFM_ACCOUNTING.Business.Settings;
using TFM_ACCOUNTING.Data;

namespace TFM_ACCOUNTING.Business;

public sealed class ApplicationServices
{
    public ApplicationServices(string? connectionString = null)
    {
        var connection = connectionString ?? AccountingDatabase.ResolveConnectionString();
        var factory = AccountingDatabase.CreateFactory(connection);
        Obligations = new ObligationService(factory);
        AttachmentFolder = new AttachmentFolderSettings();
        Attachments = new AttachmentService(factory, AttachmentFolder);
        OwnCompanies = new OwnCompanyService(factory);
        Partners = new PartnerService(factory);
        Settings = new SettingsServiceFactory(factory);
        var description = new SqlConnectionStringBuilder(connection);
        DatabaseDescription = description.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            ? $"Helyi tesztadatbázis • {description.InitialCatalog}"
            : $"Adatbázis • {description.InitialCatalog}";
    }

    public ObligationService Obligations { get; }
    public AttachmentFolderSettings AttachmentFolder { get; }
    public AttachmentService Attachments { get; }
    public IOwnCompanyService OwnCompanies { get; }
    public IPartnerService Partners { get; }
    public SettingsServiceFactory Settings { get; }
    public string DatabaseDescription { get; }
}

