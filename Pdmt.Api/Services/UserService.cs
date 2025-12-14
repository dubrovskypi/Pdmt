namespace Pdmt.Api.Services
{
    public class UserService : IUserService
    {
        private readonly Guid _testUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid GetUserId()
        {
            return _testUserId;
        }
    }
}
