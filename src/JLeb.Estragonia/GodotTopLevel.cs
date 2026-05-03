using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Styling;

namespace JLeb.Estragonia;

/// <summary>
/// A <see cref="TopLevel"/> used with Godot.
/// This is implicitly created by <see cref="AvaloniaControl"/>.
/// </summary>
public sealed class GodotTopLevel : EmbeddableControlRoot {

	internal GodotTopLevelImpl Impl { get; }

	static GodotTopLevel() {
		// TopLevel has Cycle navigation mode but we want the focus to be able to leave Avalonia to return back to godot: use Continue
		KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<GodotTopLevel>(KeyboardNavigationMode.Continue);

		// Provide a default template matching the structure used by Fluent/Simple themes.
		// EmbeddableControlRoot has no theme template from Semi/Ursa, so we supply one here.
		// The template contains PART_VisualLayerManager so overlay popups can be hosted.
		TemplateProperty.OverrideDefaultValue<GodotTopLevel>(
			new FuncControlTemplate<GodotTopLevel>((_, _) =>
				new Panel {
					Children = {
						new Border {
							Name = "PART_TransparencyFallback",
							IsHitTestVisible = false,
						},
						new Border {
							[!BackgroundProperty] = new TemplateBinding(BackgroundProperty),
							Child = new VisualLayerManager {
								Name = "PART_VisualLayerManager",
								Child = new ContentPresenter {
									Name = "PART_ContentPresenter",
									[!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
									[!ContentPresenter.ContentTemplateProperty] = new TemplateBinding(ContentControl.ContentTemplateProperty),
									[!MarginProperty] = new TemplateBinding(PaddingProperty),
								},
							},
						},
					},
				}
			)
		);
	}

	internal GodotTopLevel(GodotTopLevelImpl impl)
		: base(impl)
		=> Impl = impl;

}
