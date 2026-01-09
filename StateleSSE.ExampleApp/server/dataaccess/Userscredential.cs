namespace dataaccess;

public partial class Userscredential
{
    public string Id { get; set; } = null!;

    public string Salt { get; set; } = null!;

    public string Passwordhash { get; set; } = null!;

    public virtual User IdNavigation { get; set; } = null!;
}