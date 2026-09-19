using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SubiektNexoConnector.Api.Idempotency;

/// <summary>Business controller writes are protected; technical minimal endpoints are not.</summary>
public static class IdempotencyPolicy
{
    public static bool Requires(string method, ActionDescriptor? action) =>
        action is ControllerActionDescriptor &&
        (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
         || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method));

    public static string IndeterminateDetail(string method)
    {
        const string prefix = "Do not resubmit with a new key. ";
        if (HttpMethods.IsDelete(method))
            return prefix + "Check whether the resource still exists to verify whether deletion succeeded.";
        if (HttpMethods.IsPatch(method) || HttpMethods.IsPut(method))
            return prefix + "Check the current resource data to verify whether the requested changes were applied.";
        return prefix + "Check the resource list to verify whether the resource was created.";
    }
}
