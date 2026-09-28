using System;
using eZakazivanje.DataService.Data;

namespace eZakazivanje.DataService.Repositories.Interfaces;

 public interface IUnitOfWork
 {
    IAddressRepository Addresses { get; }
    IBussinessRepository Bussinesses { get; }
    IUserRepository Users { get; }
    IAppointmentRepository Appointments { get; }
    ICategoryRepository Categories { get; }
    ICommentRepository Comments { get; }
    IEmployeeRepository Employees { get; }
    IServiceRepository Services { get; }
    ISubscriptionRepository Subscriptions { get; }
    AppDbContext Context { get; }

     Task<bool> CompleteAsync();

 }
