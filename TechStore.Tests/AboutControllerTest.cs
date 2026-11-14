using Microsoft.AspNetCore.Mvc;
using TechStore.Controllers;
using TechStore.Models;

namespace TechStore.Tests;

public class AboutControllerTests
{
    [Fact]
    public void Index_DevuelveVistaConAboutViewModel()
    {
        var controller = new AboutController();

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<AboutViewModel>(view.Model);
    }

    [Fact]
    public void Index_DevuelveTresEventosDeLineaDeTiempoOrdenadosPorAnio()
    {
        var controller = new AboutController();

        var result = controller.Index();

        var model = Assert.IsType<AboutViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(3, model.Timeline.Count);
        Assert.Equal(model.Timeline.Select(t => t.Year).OrderBy(y => y), model.Timeline.Select(t => t.Year));
    }

    [Fact]
    public void Index_DevuelveTresMiembrosDelEquipoConFoto()
    {
        var controller = new AboutController();

        var result = controller.Index();

        var model = Assert.IsType<AboutViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(3, model.Team.Count);
        Assert.All(model.Team, m => Assert.False(string.IsNullOrWhiteSpace(m.PhotoUrl)));
    }
}
