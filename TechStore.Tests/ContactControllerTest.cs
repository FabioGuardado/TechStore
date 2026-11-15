using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TechStore.Controllers;

namespace TechStore.Tests;

public class ContactControllerTests
{
    [Fact]
    public void Index_DevuelveViewResult()
    {
        var controller = new ContactController(NullLogger<ContactController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }
}
