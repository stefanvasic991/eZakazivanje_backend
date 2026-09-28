using System;
using AutoMapper;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;

namespace eZakazivanje.Api.MappingProfiles;

public class RequestToDomain : Profile
{
    public RequestToDomain()
    {
        CreateMap<CreateBussiness, Bussiness>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.ImageUrls, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        CreateMap<CreateCategory, Category>()  
        .ForMember(dest => dest.Name, opt => 
            opt.MapFrom(src => src.Name))
        .ForMember(dest => dest.Description, opt => 
            opt.MapFrom(src => src.Description))
        .ForMember(dest => dest.ImageUrl, opt => 
            opt.MapFrom(src => src.ImageUrl))
        .ForMember(dest => dest.CreatedAt, opt => 
            opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => 
            opt.MapFrom(src => DateTime.UtcNow))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateAddress, Address>()
        .ForMember(dest => dest.StreetName, opt => opt.MapFrom(src => src.StreetName))
        .ForMember(dest => dest.Floor, opt => opt.MapFrom(src => src.Floor))
        .ForMember(dest => dest.ZipCode, opt => opt.MapFrom(src => src.ZipCode))
        .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City))
        .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateAppointment, Appointment>()
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.AppointmentDate, opt => opt.MapFrom(src => src.AppointmentDate))
        .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
        .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime))
        .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
        .ForMember(dest => dest.IsCancelled, opt => opt.MapFrom(src => src.IsCancelled))
        .ForMember(dest => dest.CancellationReason, opt => opt.MapFrom(src => src.CancellationReason))
        .ForMember(dest => dest.Id, opt => opt.Ignore())
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateComment, Comment>()
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Content))
        .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.Rating))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateEmployee, Employee>()
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.IsAvailable))
        .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
        .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
        .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateService, Service>()
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
        .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.Duration))
        .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
        .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Currency))
        .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl))
        .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateUser, ApplicationUser>()
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
        .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
        .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
        //.ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
        //.ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
        .ForAllMembers(opts =>
        {
            opts.AllowNull();
            opts.Condition((src, dest, srcMember) => srcMember != null);
        });

        CreateMap<CreateBusinessAddressDto, Address>();
    }

}
