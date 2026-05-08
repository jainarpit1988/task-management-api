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

        CreateMap<TaskItem, TaskListItemDto>();

        CreateMap<TaskUpdate, TaskUpdateDto>();
        CreateMap<TaskAssignment, TaskAssignmentDto>();
        CreateMap<TaskAcknowledgement, TaskAcknowledgementDto>();

        CreateMap<TaskItem, TaskDetailsDto>()
            .ForMember(d => d.Updates, opt => opt.MapFrom(s => s.Updates.OrderByDescending(x => x.CreatedAt)))
            .ForMember(d => d.Assignments, opt => opt.MapFrom(s => s.Assignments.OrderByDescending(x => x.AssignedAt)))
            .ForMember(d => d.Acknowledgements, opt => opt.MapFrom(s => s.Acknowledgements.OrderByDescending(x => x.AcknowledgedAt)));
    }
}

