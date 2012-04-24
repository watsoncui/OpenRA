#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using OpenRA;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class CheatsLogic
	{
		[ObjectCreator.UseCtor]
		public CheatsLogic(Widget widget, Action onExit, World world)
		{
			var devTrait = world.LocalPlayer.PlayerActor.Trait<DeveloperMode>();

			var shroudCheckbox = widget.GetWidget<CheckboxWidget>("DISABLE_SHROUD");
			shroudCheckbox.IsChecked = () => devTrait.DisableShroud;
			shroudCheckbox.OnClick = () => Order(world, "DevShroud");

			var pathCheckbox = widget.GetWidget<CheckboxWidget>("SHOW_UNIT_PATHS");
			pathCheckbox.IsChecked = () => devTrait.PathDebug;
			pathCheckbox.OnClick = () => Order(world, "DevPathDebug");

			widget.GetWidget<ButtonWidget>("GIVE_CASH").OnClick = () =>
				world.IssueOrder(new Order("DevGiveCash", world.LocalPlayer.PlayerActor, false));

			var fastBuildCheckbox = widget.GetWidget<CheckboxWidget>("INSTANT_BUILD");
			fastBuildCheckbox.IsChecked = () => devTrait.FastBuild;
			fastBuildCheckbox.OnClick = () => Order(world, "DevFastBuild");

			var fastChargeCheckbox = widget.GetWidget<CheckboxWidget>("INSTANT_CHARGE");
			fastChargeCheckbox.IsChecked = () => devTrait.FastCharge;
			fastChargeCheckbox.OnClick = () => Order(world, "DevFastCharge");

			var allTechCheckbox = widget.GetWidget<CheckboxWidget>("ENABLE_TECH");
			allTechCheckbox.IsChecked = () => devTrait.AllTech;
			allTechCheckbox.OnClick = () => Order(world, "DevEnableTech");

			var powerCheckbox = widget.GetWidget<CheckboxWidget>("UNLIMITED_POWER");
			powerCheckbox.IsChecked = () => devTrait.UnlimitedPower;
			powerCheckbox.OnClick = () => Order(world, "DevUnlimitedPower");

			var buildAnywhereCheckbox = widget.GetWidget<CheckboxWidget>("BUILD_ANYWHERE");
			buildAnywhereCheckbox.IsChecked = () => devTrait.BuildAnywhere;
			buildAnywhereCheckbox.OnClick = () => Order(world, "DevBuildAnywhere");

			widget.GetWidget<ButtonWidget>("GIVE_EXPLORATION").OnClick = () =>
				world.IssueOrder(new Order("DevGiveExploration", world.LocalPlayer.PlayerActor, false));

			widget.GetWidget<ButtonWidget>("CLOSE").OnClick = () => { Ui.CloseWindow(); onExit(); };
		}

		public void Order(World world, string order)
		{
			world.IssueOrder(new Order(order, world.LocalPlayer.PlayerActor, false));
		}
	}
}