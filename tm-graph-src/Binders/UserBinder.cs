using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using tm_graph_src.Exceptions;
using tm_graph_src.Options;

namespace tm_graph_src.Binders;

public class UserBinder : IModelBinder
{
    /// <summary>
    /// Binds a model from the current HTTP context, mapping the context's user item to the specified model if available.
    /// </summary>
    /// <param name="bindingContext">
    /// The context within which model binding occurs, containing information such as HTTP context, model name, and model state.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous model binding operation.
    /// If successful, the <see cref="ModelBindingResult"/> is set to the user object from the HTTP context; otherwise, an exception is thrown.
    /// </returns>
    /// <exception cref="InternalServerErrorException">
    /// Thrown when authentication fails due to the absence of a user in the HTTP context or if authentication data was corrupted or overwritten.
    /// </exception>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        var maybeUser = bindingContext.HttpContext.Items[SessionAuthenticationOptions.ContextItemUserKey];
        if (maybeUser is null) throw new InternalServerErrorException("Authentication failed");
        if (maybeUser is User user)
        {
            bindingContext.Result = ModelBindingResult.Success(user);
            return Task.CompletedTask;
        }
        else
        {
            throw new InternalServerErrorException("Authentication data was overwritten");
        }
    }
}

public class UserBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(User))
        {
            return new BinderTypeModelBinder(typeof(UserBinder));
        }
        return null;
    }
}