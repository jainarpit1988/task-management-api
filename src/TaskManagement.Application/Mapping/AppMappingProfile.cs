using AutoMapper;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Mapping;

public sealed class AppMappingProfile : Profile
{
    public AppMappingProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<TaskItem, TaskListItemDto>()
            .ForMember(d => d.PdStatus, opt => opt.MapFrom(s => s.StatusLookupId))
            .ForMember(d => d.TaskStatusLookupId, opt => opt.MapFrom(s => s.QueryStatusLookupId))
            .ForMember(d => d.OtherText, opt => opt.MapFrom(s => s.TaskStatusOther));

        CreateMap<TaskUpdate, TaskUpdateDto>();
        CreateMap<TaskAssignment, TaskAssignmentDto>();
        CreateMap<TaskAcknowledgement, TaskAcknowledgementDto>();

        CreateMap<TaskItem, TaskDetailsDto>()
            .ForMember(d => d.PdStatus, opt => opt.MapFrom(s => s.StatusLookupId))
            .ForMember(d => d.TaskStatusLookupId, opt => opt.MapFrom(s => s.QueryStatusLookupId))
            .ForMember(d => d.OtherText, opt => opt.MapFrom(s => s.TaskStatusOther))
            .ForMember(d => d.Updates, opt => opt.MapFrom(s => s.Updates.OrderByDescending(x => x.CreatedAt)))
            .ForMember(d => d.Assignments, opt => opt.MapFrom(s => s.Assignments.OrderByDescending(x => x.AssignedAt)))
            .ForMember(d => d.Acknowledgements, opt => opt.MapFrom(s => s.Acknowledgements.OrderByDescending(x => x.AcknowledgedAt)));
    }
}
