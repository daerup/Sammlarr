using Microsoft.Playwright;

namespace E2E.Tests;

internal static class Locators
{
	internal const string MarkListeningInputs = """
		const addEventListener = EventTarget.prototype.addEventListener;
		EventTarget.prototype.addEventListener = function (type, listener, options) {
			if (type === 'input' && this instanceof HTMLInputElement) {
				this.dataset.e2eListening = '';
			}
			return addEventListener.call(this, type, listener, options);
		};
		""";

	private const string ListeningInput = "[data-e2e-listening]";

	extension(IPage page)
	{
		internal ILocator Button(string name) => page.GetByRole(AriaRole.Button, Exact(name));

		internal ILocator ButtonContaining(string name) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name });

		internal ILocator Heading(string name) => page.GetByRole(AriaRole.Heading, Exact(name));

		internal ILocator Row(string name) => page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name });

		internal ILocator FeedCard(string name) => page.GetByRole(AriaRole.Article, Exact(name));

		internal ILocator Option(string name) => page.GetByRole(AriaRole.Option, Exact(name));

		internal ILocator OptionContaining(string name) => page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = name });

		internal ILocator MenuItem(string name) => page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = name });

		internal ILocator Link(string name) => page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name });

		internal ILocator AlertDialog => page.GetByRole(AriaRole.Alertdialog);

		internal ILocator Modal(string name) => page.GetByRole(AriaRole.Dialog, Exact(name));

		internal ILocator Field(string label) => page.Locator("[data-slot='field']").Filter(new LocatorFilterOptions { HasText = label });

		internal ILocator Text(string value) => page.GetByText(value, new PageGetByTextOptions { Exact = true });
	}

	extension(ILocator scope)
	{
		internal ILocator Button(string name) => scope.GetByRole(AriaRole.Button, ExactIn(name));

		internal ILocator ButtonContaining(string name) => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name });

		internal ILocator Radio(string name) => scope.GetByRole(AriaRole.Radio, ExactIn(name));

		internal ILocator Combobox => scope.GetByRole(AriaRole.Combobox);

		internal ILocator Switch => scope.GetByRole(AriaRole.Switch);

		internal ILocator Text(string value) => scope.GetByText(value, new LocatorGetByTextOptions { Exact = true });

		internal ILocator Labelled(string label) => scope.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

		internal async Task EnterAsync(string value)
		{
			var input = scope.And(scope.Page.Locator(ListeningInput));
			await input.FillAsync(value);
			await input.PressAsync("Tab");
		}
	}

	private static PageGetByRoleOptions Exact(string name) => new() { Name = name, Exact = true };

	private static LocatorGetByRoleOptions ExactIn(string name) => new() { Name = name, Exact = true };
}
