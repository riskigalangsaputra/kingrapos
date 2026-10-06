using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Tests;

public class SupplierServiceTests
{
    [Fact]
    public async Task Create_then_list_returns_the_supplier()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ISupplierService>();
        var created = await service.CreateAsync(new SupplierRequest(
            "PT Sumber Rejeki",
            "021123456",
            "sales@sumber.co.id",
            "Jakarta",
            null));

        Assert.Equal("PT Sumber Rejeki", created.Name);
        Assert.Equal(0, created.ContactCount);

        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task Create_requires_the_supplier_manage_permission()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var service = database.GetRequiredService<ISupplierService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(new SupplierRequest("PT X", "021", null, null, null)));
    }

    [Fact]
    public async Task Duplicate_name_and_phone_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ISupplierService>();
        await service.CreateAsync(new SupplierRequest("PT Sumber", "021123", null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new SupplierRequest("PT Sumber", "021123", null, null, null)));

        // nama sama tetapi nomor berbeda tetap boleh
        var other = await service.CreateAsync(new SupplierRequest("PT Sumber", "021999", null, null, null));
        Assert.Equal("021999", other.PhoneNumber);
    }

    [Fact]
    public async Task Contacts_can_be_added_and_marked_primary()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ISupplierService>();
        var supplier = await service.CreateAsync(new SupplierRequest("PT Sumber", "021123", null, null, null));

        var first = await service.AddContactAsync(supplier.Id, new SupplierContactRequest(
            "Budi", "0811", "Sales", IsPrimary: true));

        var second = await service.AddContactAsync(supplier.Id, new SupplierContactRequest(
            "Andi", "0812", "Sales", IsPrimary: true));

        Assert.NotEqual(first.Id, second.Id);

        var contacts = await service.GetContactsAsync(supplier.Id);

        Assert.Equal(2, contacts.Count);
        Assert.Single(contacts.Where(contact => contact.IsPrimary));
        Assert.Equal("Andi", contacts.Single(contact => contact.IsPrimary).Name);

        var listed = (await service.GetAllAsync()).Single();
        Assert.Equal(2, listed.ContactCount);
    }

    [Fact]
    public async Task Removing_a_contact_soft_deletes_it()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ISupplierService>();
        var supplier = await service.CreateAsync(new SupplierRequest("PT Sumber", "021123", null, null, null));
        var contact = await service.AddContactAsync(supplier.Id, new SupplierContactRequest(
            "Budi", null, null, IsPrimary: false));

        await service.RemoveContactAsync(contact.Id);

        Assert.Empty(await service.GetContactsAsync(supplier.Id));
    }

    [Fact]
    public async Task Delete_soft_deletes_the_supplier_and_its_contacts()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ISupplierService>();
        var supplier = await service.CreateAsync(new SupplierRequest("PT Sementara", "021777", null, null, null));
        await service.AddContactAsync(supplier.Id, new SupplierContactRequest("Budi", null, null, false));

        await service.DeleteAsync(supplier.Id);

        Assert.Empty(await service.GetAllAsync());

        // nama + nomor yang sama bisa dipakai lagi setelah dihapus
        var recreated = await service.CreateAsync(new SupplierRequest("PT Sementara", "021777", null, null, null));
        Assert.NotEqual(supplier.Id, recreated.Id);
    }
}
