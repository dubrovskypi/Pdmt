using FluentValidation;

namespace Pdmt.Api.Dto.Validation
{
    public class CreateEventDtoValidator : AbstractValidator<EventDto>
    {
        public CreateEventDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Intensity)
                .InclusiveBetween(0, 10);

            RuleFor(x => x.Timestamp)
                .LessThanOrEqualTo(DateTime.UtcNow);
        }
    }
}
