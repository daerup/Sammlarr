using Microsoft.Playwright;

namespace E2E.Tests;

internal static class Locators
{
	extension(IPage page)
	{
		internal ILocator Button(string name) => page.GetByRole(AriaRole.Button, Exact(name));

		internal ILocator ButtonContaining(string name) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name });

		internal ILocator Heading(string name) => page.GetByRole(AriaRole.Heading, Exact(name));

		internal ILocator Row(string name) => page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name });

		internal ILocator Cell(string name) => page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = name });

		internal ILocator Option(string name) => page.GetByRole(AriaRole.Option, Exact(name));

		internal ILocator OptionContaining(string name) => page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = name });

		internal ILocator Combobox => page.GetByRole(AriaRole.Combobox);

		internal ILocator MenuItem(string name) => page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = name });

		internal ILocator Link(string name) => page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name });

		internal ILocator AlertDialog => page.GetByRole(AriaRole.Alertdialog);

		internal ILocator Field(string label) => page.Locator("[data-slot='field']").Filter(new LocatorFilterOptions { HasText = label });

		internal ILocator Text(string value) => page.GetByText(value, new PageGetByTextOptions { Exact = true });
	}

	extension(ILocator scope)
	{
		internal ILocator Button(string name) => scope.GetByRole(AriaRole.Button, ExactIn(name));

		internal ILocator ButtonContaining(string name) => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name });

		internal ILocator Heading(string name) => scope.GetByRole(AriaRole.Heading, ExactIn(name));

		internal ILocator Option(string name) => scope.GetByRole(AriaRole.Option, ExactIn(name));

		internal ILocator Combobox => scope.GetByRole(AriaRole.Combobox);

		internal ILocator Switch => scope.GetByRole(AriaRole.Switch);

		internal ILocator Checkbox => scope.GetByRole(AriaRole.Checkbox);

		internal ILocator Text(string value) => scope.GetByText(value, new LocatorGetByTextOptions { Exact = true });
	}

	private static PageGetByRoleOptions Exact(string name) => new() { Name = name, Exact = true };

	private static LocatorGetByRoleOptions ExactIn(string name) => new() { Name = name, Exact = true };
}
