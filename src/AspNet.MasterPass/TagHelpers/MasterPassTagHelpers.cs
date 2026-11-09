using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace AspNet.MasterPass.TagHelpers;

/// <summary>
/// Renders its contents only when the current session was created with the master password.
/// </summary>
[HtmlTargetElement("master-pass-only", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class MasterPassOnlyTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current request.</param>
    public MasterPassOnlyTagHelper(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (_httpContextAccessor.HttpContext.IsMasterPassLogin())
        {
            output.TagName = null;
            return;
        }

        output.SuppressOutput();
    }
}

/// <summary>
/// Renders its contents only when the current session used the account's own password.
/// </summary>
[HtmlTargetElement("real-password-only", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class RealPasswordOnlyTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current request.</param>
    public RealPasswordOnlyTagHelper(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (_httpContextAccessor.HttpContext.IsMasterPassLogin())
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = null;
    }
}