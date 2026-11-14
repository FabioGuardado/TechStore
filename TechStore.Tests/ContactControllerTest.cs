using Microsoft.AspNetCore.Mvc;
using TechStore.Controllers;

namespace TechStore.Tests;

public class ContactControllerTests
{
    [Fact]
    public void Index_DevuelveViewResult()
    {
        var controller = new ContactController();

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }
}
