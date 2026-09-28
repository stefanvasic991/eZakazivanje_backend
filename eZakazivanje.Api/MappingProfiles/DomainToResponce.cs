using System;
using AutoMapper;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Responce;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.Api.MappingProfiles;

public class DomainToResponce : Profile
{
    public DomainToResponce()
    {
        CreateMap<Bussiness, CreateBussinessResponce>()
        .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
        .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
        .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
        .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
        .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt))
        .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.ImageUrls));

        CreateMap<Subscription, SubscriptionResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.BusinessId, opt => opt.MapFrom(src => src.BusinessId))
            .ForMember(dest => dest.SubscriptionPlanId, opt => opt.MapFrom(src => src.SubscriptionPlanId))
            .ForMember(dest => dest.Platform, opt => opt.MapFrom(src => src.Platform))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.AutoRenewDate, opt => opt.MapFrom(src => src.AutoRenewDate))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.IsAutoRenewing, opt => opt.MapFrom(src => src.IsAutoRenewing))
            .ForMember(dest => dest.IsCancelled, opt => opt.MapFrom(src => src.IsCancelled))
            .ForMember(dest => dest.CancelledDate, opt => opt.MapFrom(src => src.CancelledDate))
            .ForMember(dest => dest.VerificationStatus, opt => opt.MapFrom(src => src.VerificationStatus))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt));

        CreateMap<SubscriptionPlan, SubscriptionPlanResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.DurationDays, opt => opt.MapFrom(src => src.DurationDays))
            .ForMember(dest => dest.Platform, opt => opt.MapFrom(src => src.Platform))
            .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));
    }
}
