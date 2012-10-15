﻿/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Caliburn.Micro;
using Caliburn.Micro.Contrib;
using Caliburn.Micro.Contrib.Dialogs;
using Caliburn.Micro.Contrib.Results;
using Gibbed.Borderlands2.FileFormats.Items;

namespace Gibbed.Borderlands2.SaveEdit
{
    [Export(typeof(BankViewModel))]
    internal class BankViewModel : PropertyChangedBase, IHandle<SaveUnpackMessage>, IHandle<SavePackMessage>
    {
        #region Fields
        private FileFormats.SaveFile _SaveFile;
        private readonly ObservableCollection<IBaseSlotViewModel> _Slots;

        private IBaseSlotViewModel _SelectedSlot;
        #endregion

        #region Properties
        public ObservableCollection<IBaseSlotViewModel> Slots
        {
            get { return this._Slots; }
        }

        public IBaseSlotViewModel SelectedSlot
        {
            get { return this._SelectedSlot; }
            set
            {
                this._SelectedSlot = value;
                this.NotifyOfPropertyChange(() => this.SelectedSlot);
            }
        }
        #endregion

        [ImportingConstructor]
        public BankViewModel(IEventAggregator events)
        {
            this._Slots = new ObservableCollection<IBaseSlotViewModel>();
            events.Subscribe(this);
        }

        public void NewWeapon()
        {
            var weapon = new BaseWeapon()
            {
                UniqueId = new Random().Next(int.MinValue, int.MaxValue),
                // TODO: check other item unique IDs to prevent rare collisions
                AssetLibrarySetId = 0,
            };
            var viewModel = new BaseWeaponViewModel(weapon);
            this.Slots.Add(viewModel);
            this.SelectedSlot = viewModel;
        }

        public void NewItem()
        {
            var item = new BaseItem()
            {
                UniqueId = new Random().Next(int.MinValue, int.MaxValue),
                // TODO: check other item unique IDs to prevent rare collisions
                AssetLibrarySetId = 0,
            };
            var viewModel = new BaseItemViewModel(item);
            this.Slots.Add(viewModel);
            this.SelectedSlot = viewModel;
        }

        private static readonly Regex _CodeSignature =
            new Regex(@"BL2\((?<data>(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?)\)",
                      RegexOptions.CultureInvariant | RegexOptions.Multiline);

        public IEnumerable<IResult> PasteCode()
        {
            if (Clipboard.ContainsText(TextDataFormat.Text) != true &&
                Clipboard.ContainsText(TextDataFormat.UnicodeText) != true)
            {
                yield break;
            }

            var errors = 0;
            var viewModels = new List<IBaseSlotViewModel>();
            yield return new DelegateResult(() =>
            {
                var codes = Clipboard.GetText();

                // strip whitespace
                codes = Regex.Replace(codes, @"\s+", "");

                foreach (var match in _CodeSignature.Matches(codes).Cast<Match>()
                    .Where(m => m.Success == true))
                {
                    var code = match.Groups["data"].Value;

                    IPackable packable;

                    try
                    {
                        var data = Convert.FromBase64String(code);
                        packable = BaseDataHelper.Decode(data);
                    }
                    catch (Exception)
                    {
                        errors++;
                        continue;
                    }

                    // TODO: check other item unique IDs to prevent rare collisions
                    packable.UniqueId = new Random().Next(int.MinValue, int.MaxValue);

                    if (packable is BaseWeapon)
                    {
                        var weapon = (BaseWeapon)packable;
                        var viewModel = new BaseWeaponViewModel(weapon);
                        viewModels.Add(viewModel);
                    }
                    else if (packable is BaseItem)
                    {
                        var item = (BaseItem)packable;
                        var viewModel = new BaseItemViewModel(item);
                        viewModels.Add(viewModel);
                    }
                }
            });

            if (viewModels.Count > 0)
            {
                viewModels.ForEach(vm => this.Slots.Add(vm));
                this.SelectedSlot = viewModels.First();
            }

            if (errors > 0)
            {
                yield return
                    new MyMessageBox("Failed to load " + errors.ToString(CultureInfo.InvariantCulture) + " codes.",
                                     "Warning")
                        .WithIcon(MessageBoxImage.Warning);
            }
        }

        public void CopySelectedSlotCode()
        {
            if (this.SelectedSlot == null ||
                !(this.SelectedSlot.BaseSlot is IPackable))
            {
                Clipboard.SetText("", TextDataFormat.Text);
                return;
            }

            // just a hack until I add a way to override the unique ID in Encode()
            var copy = (IPackable)this.SelectedSlot.BaseSlot.Clone();
            copy.UniqueId = new Random().Next(int.MinValue, int.MaxValue);

            var data = BaseDataHelper.Encode(copy);
            var sb = new StringBuilder();
            sb.Append("BL2(");
            sb.Append(Convert.ToBase64String(data, Base64FormattingOptions.None));
            sb.Append(")");
            Clipboard.SetText(sb.ToString(), TextDataFormat.Text);
        }

        public void DuplicateSelectedSlot()
        {
            if (this.SelectedSlot == null)
            {
                return;
            }

            var copy = (IBaseSlot)this.SelectedSlot.BaseSlot.Clone();
            copy.UniqueId = new Random().Next(int.MinValue, int.MaxValue);
            // TODO: check other item unique IDs to prevent rare collisions

            if (copy is BaseWeapon)
            {
                var weapon = (BaseWeapon)copy;

                var viewModel = new BaseWeaponViewModel(weapon);
                this.Slots.Add(viewModel);
                this.SelectedSlot = viewModel;
            }
            else if (copy is BaseItem)
            {
                var item = (BaseItem)copy;

                var viewModel = new BaseItemViewModel(item);
                this.Slots.Add(viewModel);
                this.SelectedSlot = viewModel;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public void UnbankSelectedSlot()
        {
            // TODO: implement me
        }

        public void DeleteSelectedSlot()
        {
            if (this.SelectedSlot == null)
            {
                return;
            }

            this.Slots.Remove(this.SelectedSlot);
            this.SelectedSlot = this.Slots.FirstOrDefault();
        }

        public void Handle(SaveUnpackMessage message)
        {
            this._SaveFile = message.SaveFile;

            this.Slots.Clear();

            foreach (var bankSlot in this._SaveFile.SaveGame.BankSlots)
            {
                var slot = BaseDataHelper.Decode(bankSlot.Data);
                var test = BaseDataHelper.Encode(slot);
                if (Enumerable.SequenceEqual(bankSlot.Data, test) == false)
                {
                    throw new FormatException();
                }

                if (slot is BaseWeapon)
                {
                    var viewModel = new BaseWeaponViewModel((BaseWeapon)slot);
                    this.Slots.Add(viewModel);
                }
                else if (slot is BaseItem)
                {
                    var viewModel = new BaseItemViewModel((BaseItem)slot);
                    this.Slots.Add(viewModel);
                }
            }
        }

        public void Handle(SavePackMessage message)
        {
            var saveFile = message.SaveFile;

            saveFile.SaveGame.BankSlots.Clear();

            foreach (var viewModel in this.Slots)
            {
                var slot = viewModel.BaseSlot;

                if (slot is BaseWeapon)
                {
                    var weapon = (BaseWeapon)slot;
                    var data = BaseDataHelper.Encode(weapon);

                    saveFile.SaveGame.BankSlots.Add(new ProtoBufFormats.WillowTwoSave.BankSlot()
                    {
                        Data = data,
                    });
                }
                else if (slot is BaseItem)
                {
                    var item = (BaseItem)slot;
                    var data = BaseDataHelper.Encode(item);

                    saveFile.SaveGame.BankSlots.Add(new ProtoBufFormats.WillowTwoSave.BankSlot()
                    {
                        Data = data,
                    });
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}