using auth.Application.Abstractions;
using auth.Application.Commands.ApiKeys;
using auth.Application.Commands.Auth;
using auth.Application.Handlers.ApiKeys;
using auth.Application.Handlers.Auth;
using auth.Application.Handlers.Jwks;
using auth.Application.Queries.ApiKeys;
using auth.Application.Queries.Auth;
using auth.Application.Queries.Jwks;
using auth.Application.Validators.ApiKeys;
using auth.Application.Validators.Auth;
using auth.Domain.Entities;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using Moq;

namespace Auth.Tests;

public sealed class ApplicationHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Register_trims_login_hashes_password_and_creates_user_and_session()
    {
        var users = new Mock<IUserRepository>();
        var sessions = new Mock<ISessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var jwt = JwtMock();
        var unit = UnitOfWorkMock();
        User? addedUser = null;
        Session? addedSession = null;
        users.Setup(x => x.GetByLoginAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        hasher.Setup(x => x.Hash("secret")).Returns("hashed");
        users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => addedUser = u)
            .Returns(Task.CompletedTask);
        sessions.Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => addedSession = s)
            .Returns(Task.CompletedTask);

        var result = await new RegisterHandler(
            users.Object, hasher.Object, sessions.Object, jwt.Object, unit.Object)
            .Handle(new RegisterCommand("  alice  ", "secret"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("alice", addedUser!.Login);
        Assert.Equal("hashed", addedUser.PasswordHash);
        Assert.NotEqual(Guid.Empty, addedUser.Id);
        Assert.Equal(addedUser.Id, addedSession!.UserId);
        Assert.Equal("refresh-jti", addedSession.RefreshTokenJti);
        unit.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_rejects_existing_login_without_writing()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByLoginAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Login = "alice" });
        var unit = UnitOfWorkMock();

        var result = await new RegisterHandler(
            users.Object, Mock.Of<IPasswordHasher>(), Mock.Of<ISessionRepository>(),
            JwtMock().Object, unit.Object).Handle(new RegisterCommand(" alice ", "secret"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.LoginAlreadyExists", result.Error.Code);
        unit.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_rejects_unknown_user_and_wrong_password_without_creating_session()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByLoginAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sessions = new Mock<ISessionRepository>();

        var result = await new LoginHandler(
            users.Object, sessions.Object, Mock.Of<IPasswordHasher>(),
            JwtMock().Object, UnitOfWorkMock().Object)
            .Handle(new LoginCommand(" alice ", "bad"), default);

        Assert.Equal("Auth.InvalidCredentials", result.Error.Code);
        sessions.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_creates_session_for_valid_credentials()
    {
        var users = new Mock<IUserRepository>();
        var sessions = new Mock<ISessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var unit = UnitOfWorkMock();
        users.Setup(x => x.GetByLoginAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Login = "alice", PasswordHash = "hash" });
        hasher.Setup(x => x.Verify("secret", "hash")).Returns(true);

        var result = await new LoginHandler(
            users.Object, sessions.Object, hasher.Object,
            JwtMock().Object, unit.Object).Handle(new LoginCommand(" alice ", "secret"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        sessions.Verify(x => x.AddAsync(
            It.Is<Session>(s => s.UserId == UserId && s.RefreshTokenJti == "refresh-jti"),
            It.IsAny<CancellationToken>()), Times.Once);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_returns_error_for_invalid_token()
    {
        var jwt = JwtMock();
        jwt.Setup(x => x.ValidateRefreshToken("bad"))
            .Returns(new Error("Auth.RefreshTokenInvalid", "bad", ErrorType.Unauthorized));

        var result = await new RefreshHandler(
            Mock.Of<ISessionRepository>(), jwt.Object, UnitOfWorkMock().Object)
            .Handle(new RefreshCommand("bad"), default);

        Assert.Equal("Auth.RefreshTokenInvalid", result.Error.Code);
    }

    [Fact]
    public async Task Refresh_rotates_active_session_and_revokes_old_one()
    {
        var sessions = new Mock<ISessionRepository>();
        var unit = UnitOfWorkMock();
        var jwt = JwtMock();
        var session = new Session
        {
            Id = Guid.NewGuid(), UserId = UserId, RefreshTokenJti = "old-jti",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        jwt.Setup(x => x.ValidateRefreshToken("old"))
            .Returns(new JwtValidationResult(UserId, "old-jti", DateTime.UtcNow.AddMinutes(5)));
        sessions.Setup(x => x.GetByRefreshTokenJtiAsync("old-jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        sessions.Setup(x => x.RevokeIfActiveAsync(
                "old-jti", UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new RefreshHandler(sessions.Object, jwt.Object, unit.Object)
            .Handle(new RefreshCommand("old"), default);

        Assert.True(result.IsSuccess);
        sessions.Verify(x => x.RevokeIfActiveAsync(
            "old-jti", UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        sessions.Verify(x => x.AddAsync(
            It.Is<Session>(s => s.UserId == UserId && s.RefreshTokenJti == "refresh-jti"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_fails_when_session_was_concurrently_revoked()
    {
        var jwt = JwtMock();
        var sessions = new Mock<ISessionRepository>();
        jwt.Setup(x => x.ValidateRefreshToken("token"))
            .Returns(new JwtValidationResult(UserId, "jti", DateTime.UtcNow.AddMinutes(5)));
        sessions.Setup(x => x.GetByRefreshTokenJtiAsync("jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { UserId = UserId, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) });
        sessions.Setup(x => x.RevokeIfActiveAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new RefreshHandler(sessions.Object, jwt.Object, UnitOfWorkMock().Object)
            .Handle(new RefreshCommand("token"), default);

        Assert.Equal("Auth.RefreshTokenInvalid", result.Error.Code);
    }

    [Fact]
    public async Task Logout_revokes_active_session_and_is_idempotent()
    {
        var jwt = JwtMock();
        var sessions = new Mock<ISessionRepository>();
        var unit = UnitOfWorkMock();
        jwt.Setup(x => x.ValidateRefreshToken("token"))
            .Returns(new JwtValidationResult(UserId, "jti", DateTime.UtcNow.AddMinutes(5)));
        var session = new Session { UserId = UserId, RefreshTokenJti = "jti" };
        sessions.Setup(x => x.GetByRefreshTokenJtiAsync("jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new LogoutHandler(sessions.Object, jwt.Object, unit.Object);
        Assert.True((await handler.Handle(new LogoutCommand("token"), default)).IsSuccess);
        Assert.NotNull(session.RevokedAtUtc);
        Assert.True((await handler.Handle(new LogoutCommand("token"), default)).IsSuccess);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Check_login_exists_trims_and_returns_presence()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByLoginAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Login = "alice" });

        var result = await new CheckLoginExistsHandler(users.Object)
            .Handle(new CheckLoginExistsQuery(" alice "), default);

        Assert.True(result.Value.Exists);
    }

    [Fact]
    public async Task Exchange_api_key_rejects_malformed_revoked_and_unverified_keys()
    {
        var handler = new ExchangeApiKeyHandler(
            Mock.Of<IApiKeyRepository>(), Mock.Of<IApiKeyGenerator>(),
            JwtMock().Object, UnitOfWorkMock().Object);
        var malformed = await handler.Handle(new ExchangeApiKeyCommand("not-a-key"), default);
        Assert.Equal("ApiKeys.Invalid", malformed.Error.Code);
    }

    [Fact]
    public async Task Exchange_api_key_updates_last_used_and_returns_access_token()
    {
        var keys = new Mock<IApiKeyRepository>();
        var generator = new Mock<IApiKeyGenerator>();
        var key = new ApiKey { Id = Guid.NewGuid(), UserId = UserId, Prefix = "tw_ab", SecretHash = "hash" };
        keys.Setup(x => x.GetByPrefixAsync("tw_ab", It.IsAny<CancellationToken>())).ReturnsAsync(key);
        generator.Setup(x => x.Verify(key.Id, "tw_ab_secret", "hash")).Returns(true);

        var result = await new ExchangeApiKeyHandler(
            keys.Object, generator.Object, JwtMock().Object, UnitOfWorkMock().Object)
            .Handle(new ExchangeApiKeyCommand("tw_ab_secret"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.NotNull(key.LastUsedAtUtc);
        keys.Verify(x => x.UpdateAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Exchange_api_key_rejects_revoked_key_without_updating_it()
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(), UserId = UserId, Prefix = "tw_ab",
            SecretHash = "hash", RevokedAtUtc = DateTime.UtcNow
        };
        var keys = new Mock<IApiKeyRepository>();
        keys.Setup(x => x.GetByPrefixAsync("tw_ab", It.IsAny<CancellationToken>())).ReturnsAsync(key);

        var result = await new ExchangeApiKeyHandler(
            keys.Object, Mock.Of<IApiKeyGenerator>(), JwtMock().Object, UnitOfWorkMock().Object)
            .Handle(new ExchangeApiKeyCommand("tw_ab_secret"), default);

        Assert.Equal("ApiKeys.Invalid", result.Error.Code);
        keys.Verify(x => x.UpdateAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_api_key_checks_identity_user_and_permission()
    {
        var current = new Mock<ICurrentUser>();
        current.SetupGet(x => x.UserId).Returns((Guid?)null);
        var result = await new CreateApiKeyHandler(
            Mock.Of<IApiKeyRepository>(), Mock.Of<IApiKeyPermissionRepository>(),
            Mock.Of<IUserRepository>(), Mock.Of<IApiKeyGenerator>(),
            UnitOfWorkMock().Object, current.Object)
            .Handle(new CreateApiKeyCommand("key"), default);
        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Create_api_key_generates_and_persists_secret_only_once()
    {
        var current = CurrentUser();
        var users = new Mock<IUserRepository>();
        var permissions = new Mock<IApiKeyPermissionRepository>();
        var keys = new Mock<IApiKeyRepository>();
        var generator = new Mock<IApiKeyGenerator>();
        var id = Guid.NewGuid();
        users.Setup(x => x.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId });
        permissions.Setup(x => x.GetByNameAsync("readwrite", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiKeyPermission { Id = Guid.NewGuid(), Name = "readwrite" });
        generator.Setup(x => x.Generate(It.IsAny<Guid>()))
            .Returns(new GeneratedApiKey(id, "tw_ab_secret", "tw_ab", "hash", "cret"));

        var result = await new CreateApiKeyHandler(
            keys.Object, permissions.Object, users.Object, generator.Object,
            UnitOfWorkMock().Object, current.Object)
            .Handle(new CreateApiKeyCommand("  production  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("tw_ab_secret", result.Value.Key);
        keys.Verify(x => x.AddAsync(
            It.Is<ApiKey>(k => k.Id == id && k.Name == "production" && k.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_api_key_reports_missing_user_and_missing_permission()
    {
        var current = CurrentUser();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new CreateApiKeyHandler(
            Mock.Of<IApiKeyRepository>(), Mock.Of<IApiKeyPermissionRepository>(),
            users.Object, Mock.Of<IApiKeyGenerator>(), UnitOfWorkMock().Object, current.Object);

        var missingUser = await handler.Handle(new CreateApiKeyCommand("key"), default);
        Assert.Equal("Users.NotFound", missingUser.Error.Code);

        users.Setup(x => x.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId });
        var missingPermission = await handler.Handle(new CreateApiKeyCommand("key"), default);
        Assert.Equal("ApiKeys.PermissionNotConfigured", missingPermission.Error.Code);
    }

    [Fact]
    public async Task List_and_paged_api_keys_map_active_state_and_metadata()
    {
        var current = CurrentUser();
        var keys = new Mock<IApiKeyRepository>();
        var first = new ApiKey { Id = Guid.NewGuid(), Name = "active", CreatedAtUtc = DateTime.UtcNow };
        var second = new ApiKey { Id = Guid.NewGuid(), Name = "revoked", CreatedAtUtc = DateTime.UtcNow, RevokedAtUtc = DateTime.UtcNow };
        keys.Setup(x => x.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        keys.Setup(x => x.GetByUserIdPagedAsync(UserId, 2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ApiKey>)new[] { second }, 2));

        var list = await new ListApiKeysHandler(keys.Object, current.Object)
            .Handle(new ListApiKeysQuery(), default);
        var paged = await new ListApiKeysPagedHandler(keys.Object, current.Object)
            .Handle(new ListApiKeysPagedQuery(2, 1), default);

        Assert.True(list.Value.Keys[0].IsActive);
        Assert.False(list.Value.Keys[1].IsActive);
        Assert.Equal(2, paged.Value.TotalPages);
        Assert.Equal("revoked", paged.Value.Keys[0].Name);
    }

    [Fact]
    public async Task Revoke_api_key_rejects_other_users_and_is_idempotent()
    {
        var current = CurrentUser();
        var keys = new Mock<IApiKeyRepository>();
        var key = new ApiKey { Id = Guid.NewGuid(), UserId = UserId };
        keys.Setup(x => x.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);
        var handler = new RevokeApiKeyHandler(keys.Object, UnitOfWorkMock().Object, current.Object);

        Assert.True((await handler.Handle(new RevokeApiKeyCommand(key.Id), default)).IsSuccess);
        Assert.NotNull(key.RevokedAtUtc);
        Assert.True((await handler.Handle(new RevokeApiKeyCommand(key.Id), default)).IsSuccess);
        keys.Verify(x => x.UpdateAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_api_key_does_not_revoke_a_key_owned_by_another_user()
    {
        var current = CurrentUser();
        var keys = new Mock<IApiKeyRepository>();
        var key = new ApiKey { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        keys.Setup(x => x.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);

        var result = await new RevokeApiKeyHandler(keys.Object, UnitOfWorkMock().Object, current.Object)
            .Handle(new RevokeApiKeyCommand(key.Id), default);

        Assert.Equal("ApiKeys.NotFound", result.Error.Code);
        keys.Verify(x => x.UpdateAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_api_keys_requires_authenticated_current_user()
    {
        var current = new Mock<ICurrentUser>();
        current.SetupGet(x => x.UserId).Returns((Guid?)null);

        var result = await new ListApiKeysHandler(
            Mock.Of<IApiKeyRepository>(), current.Object)
            .Handle(new ListApiKeysQuery(), default);

        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Logout_rejects_unknown_or_mismatched_session()
    {
        var jwt = JwtMock();
        jwt.Setup(x => x.ValidateRefreshToken("token"))
            .Returns(new JwtValidationResult(UserId, "jti", DateTime.UtcNow.AddMinutes(5)));
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(x => x.GetByRefreshTokenJtiAsync("jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await new LogoutHandler(sessions.Object, jwt.Object, UnitOfWorkMock().Object)
            .Handle(new LogoutCommand("token"), default);

        Assert.Equal("Auth.RefreshTokenInvalid", result.Error.Code);
        sessions.Verify(x => x.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_rejects_expired_session_before_transaction()
    {
        var jwt = JwtMock();
        jwt.Setup(x => x.ValidateRefreshToken("token"))
            .Returns(new JwtValidationResult(UserId, "jti", DateTime.UtcNow.AddMinutes(-1)));
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(x => x.GetByRefreshTokenJtiAsync("jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session
            {
                UserId = UserId, RefreshTokenJti = "jti",
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1)
            });
        var unit = UnitOfWorkMock();

        var result = await new RefreshHandler(sessions.Object, jwt.Object, unit.Object)
            .Handle(new RefreshCommand("token"), default);

        Assert.Equal("Auth.RefreshTokenInvalid", result.Error.Code);
        unit.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Permissions_and_jwks_handlers_map_repository_and_provider_values()
    {
        var permissions = new Mock<IApiKeyPermissionRepository>();
        var id = Guid.NewGuid();
        permissions.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ApiKeyPermission { Id = id, Name = "readwrite" } });
        var permissionResult = await new ListApiKeyPermissionsHandler(permissions.Object)
            .Handle(new ListApiKeyPermissionsQuery(), default);
        Assert.Equal("readwrite", permissionResult.Value.Permissions[0].Name);

        var jwt = JwtMock();
        jwt.Setup(x => x.GetPublicJwk()).Returns(new PublicJwk("RSA", "sig", "kid", "RS256", "n", "e"));
        var jwks = await new GetJwksHandler(jwt.Object).Handle(new GetJwksQuery(), default);
        Assert.Equal("kid", jwks.Value.Keys[0].Kid);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("ab", "secret")]
    [InlineData("alice", "short")]
    [InlineData("alice", "")]
    public void Register_validator_rejects_invalid_input(string login, string password)
    {
        var result = new RegisterCommandValidator().Validate(new RegisterCommand(login, password));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validators_accept_valid_auth_and_api_key_commands()
    {
        Assert.True(new RegisterCommandValidator().Validate(new RegisterCommand("alice", "secret1")).IsValid);
        Assert.True(new LoginCommandValidator().Validate(new LoginCommand("alice", "secret1")).IsValid);
        Assert.True(new RefreshCommandValidator().Validate(new RefreshCommand("token")).IsValid);
        Assert.True(new CreateApiKeyCommandValidator().Validate(new CreateApiKeyCommand("production")).IsValid);
        Assert.True(new RevokeApiKeyCommandValidator().Validate(new RevokeApiKeyCommand(Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void Validators_reject_values_over_configured_limits()
    {
        Assert.False(new RegisterCommandValidator().Validate(
            new RegisterCommand(new string('a', 51), "secret1")).IsValid);
        Assert.False(new LoginCommandValidator().Validate(
            new LoginCommand("alice", new string('p', 101))).IsValid);
        Assert.False(new CreateApiKeyCommandValidator().Validate(
            new CreateApiKeyCommand(new string('k', 101))).IsValid);
    }

    private static Mock<IJwtProvider> JwtMock()
    {
        var jwt = new Mock<IJwtProvider>();
        jwt.Setup(x => x.GenerateAccessToken(It.IsAny<Guid>()))
            .Returns(("access-token", DateTime.UtcNow.AddMinutes(10)));
        jwt.Setup(x => x.GenerateRefreshToken(It.IsAny<Guid>()))
            .Returns(("refresh-token", "refresh-jti", DateTime.UtcNow.AddDays(7)));
        return jwt;
    }

    private static Mock<IUnitOfWork> UnitOfWorkMock()
    {
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(
                (operation, cancellationToken) => operation(cancellationToken));
        return unit;
    }

    private static Mock<ICurrentUser> CurrentUser()
    {
        var current = new Mock<ICurrentUser>();
        current.SetupGet(x => x.UserId).Returns(UserId);
        return current;
    }
}
