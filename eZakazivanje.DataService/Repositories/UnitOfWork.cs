using System;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;

namespace eZakazivanje.DataService.Repositories;

 public class UnitOfWork:IUnitOfWork,IDisposable
{
    private readonly AppDbContext _context;
    
    public IAddressRepository Addresses { get; private set; }
    public IBussinessRepository Bussinesses { get; private set; }
    public IUserRepository Users { get; private set; }
    public IAppointmentRepository Appointments { get; private set; }
    public ICategoryRepository Categories { get; private set; }
    public ICommentRepository Comments { get; private set; }
    public IEmployeeRepository Employees { get; private set; }
    public IServiceRepository Services { get; private set; }
    public ISubscriptionRepository Subscriptions { get; private set; }
    public AppDbContext Context => _context;


    public UnitOfWork(AppDbContext context,ILoggerFactory loggerFactory)
    {
        _context = context;
        var logger = loggerFactory.CreateLogger("logs");

        Addresses = new AddressRepository(_context, logger);
        Bussinesses = new BussinessRepository(_context, logger);
        Users = new UserRepository(_context, logger);
        Appointments = new AppointmentRepository(_context, logger);
        Categories = new CategoryRepository(_context, logger);
        Comments = new CommentRepository(_context, logger);
        Employees = new EmployeeRepository(_context, logger);
        Services = new ServiceRepository(_context, logger);
        Subscriptions = new SubscriptionRepository(_context, loggerFactory.CreateLogger<SubscriptionRepository>());

    }

    public async Task<bool> CompleteAsync()
    {
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

}
