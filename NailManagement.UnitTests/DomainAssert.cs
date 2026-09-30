using NailManagement.Domain.Shared;

namespace NailManagement.UnitTests;

/// <summary>Khẳng định một thao tác bị Domain từ chối, và từ chối <b>ở đúng ô</b>.</summary>
internal static class DomainAssert
{
    public static DomainException RejectsField(string field, Action action)
    {
        var rejection = Assert.Throws<DomainException>(action);
        Assert.Contains(rejection.Fields, error => error.Field == field);
        return rejection;
    }
}
