namespace Celd.Domain;

public abstract class DomainException(string message) : Exception(message);
public class ValidationException(string m) : DomainException(m);
public class UnauthorizedException(string m) : DomainException(m);
public class ForbiddenException(string m) : DomainException(m);
public class NotFoundException(string m) : DomainException(m);
public class ConflictException(string m) : DomainException(m);
public class InvalidTransitionException(Status from, Status to)
    : ConflictException($"Transición inválida: {from} → {to}.");
