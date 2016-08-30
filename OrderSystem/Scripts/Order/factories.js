app.factory('generateDataPromise', [
	'$http',
	'$q',
	'dataSet',
	function ($http, $q, $dataSet) {
		function _filterMenuClass(menuClass) {
			menuClass.Addition = {
				IsSelected: false,
				Ordered: 0,
			};
			return menuClass;
		}
		function _filterMenu(menu) {
			if (menu.MenuPrice == null) {
				menu.MenuPrice = {
					Price: 0,
					Discount: 1,
				}
			}
			menu.Addition = {
				Ordered: 0,
				Remarks: [],

				OriPrice: menu.MenuPrice.Price,
				SetMeals: [],
				IsOnSale: false,
				ExcludePayDiscount: false,
				IsShowRemark: menu.Remarks != null && menu.Remarks.length > 0,
				IsRemarkCollapsed: false,
				IsSetMealCollapsed: false,
			};
			menu.MenuPrice.Price = menu.MenuPrice.Price * menu.MenuPrice.Discount;

			angular.extend(menu, {
				IsShowSetMeal: function () {
					return this.Addition.SetMeals.length > 0;
				},
				ToggleSetMeal: function () {
					this.Addition.IsSetMealCollapsed = !this.Addition.IsSetMealCollapsed;
				},

				IsShowRemark: function () {
					return this.Addition.Ordered > 0 && this.Addition.IsShowRemark;
				},
				IsRemarkCollapsed: function () {
					return this.Addition.IsRemarkCollapsed && this.Remarks.length > 0;
				},
				ToggleRemark: function () {
					this.Addition.IsRemarkCollapsed = !this.Addition.IsRemarkCollapsed;
				}
			});

			return menu;
		}
		function _filterSetMenu(menu) {
			menu.Addition = {
				Ordered: 0,
			};
			return menu;
		}
		return function () {
			return $q(function (resolve) {
				var menuOnSales, menuSetMeals, payKinds;

				$http.post('/Order/GetMenuInfos').then(function (response) {
					var data = response.data;

					$dataSet.MenuClasses = data.MenuClasses;

					$dataSet.Menus = data.Menus;

					menuOnSales = data.MenuOnSales;
					$dataSet.MenuOnSalesCount = menuOnSales.length;
					menuSetMeals = data.MenuSetMeals;

					payKinds = data.PayKinds;
					$dataSet.DiscountMethods = data.DiscountMethods;
					$dataSet.Hotel = data.Hotel;

					loadFinished();
				});
				function loadFinished() {
					for (var i in payKinds) {
						if (payKinds[i].Type == 3) {
							$dataSet.PayKinds.push(payKinds[i]);
						} else {
							$dataSet.PayKinds.splice(0, 0, payKinds[i]);
						}
					}


					for (var i in $dataSet.MenuClasses) {
						_filterMenuClass($dataSet.MenuClasses[i]);
					}

					for (var i in $dataSet.Menus) {
						var menu = $dataSet.Menus[i];
						_filterMenu(menu)

						if (menu.MenuPrice.Discount < 1 || menu.MenuPrice.ExcludePayDiscount) {
							setExcludePayKindDiscount(menu);
						}
						for (var j in menuOnSales) {
							if (menuOnSales[j].Id == menu.Id) {
								menu.Addition.IsOnSale = true;
								menu.MenuPrice.Price = menuOnSales[j].Price;
								menu.MenuPrice.Discount = 1;
								setExcludePayKindDiscount(menu);
								break;
							}
						}
						for (var j in menuSetMeals) {
							if (menuSetMeals[j].MenuSetId == menu.Id && menu.IsSetMeal) {
								menuSetMeals[j].Menu = _filterSetMenu(menuSetMeals[j].Menu);
								menuSetMeals[j].Menu.Addition.Ordered = menuSetMeals[j].Count;
								menu.Addition.SetMeals.push(menuSetMeals[j].Menu);
								setExcludePayKindDiscount(menu);
							}
						}
					}
					resolve();
				}
				// 该菜品不算入全单打折中
				function setExcludePayKindDiscount(menu) {
					menu.Addition.ExcludePayDiscount = true;
				}
			});
		}
	}]);

app.factory('dataSet', [
	'$rootScope',
	function ($rootScope) {
		var dataSet = {
			Hotel: null,
			MenuClasses: [], // 菜单分类
			Menus: [], //所有菜品
			PayKinds: [],
			DiscountMethods: null,
			MenuOnSalesCount: 0,
			Reset: function () {
				for (var i in this.MenuClasses) {
					this.MenuClasses[i].Addition.Ordered = 0;
				}
				for (var i in this.Menus) {
					this.Menus[i].Addition.Ordered = 0;
					this.Menus[i].Addition.IsRemarkCollapsed = false;
					this.Menus[i].Addition.IsSetMealCollapsed = false;
					this.Menus[i].Remarks = this.Menus[i].Remarks.concat(this.Menus[i].Addition.Remarks);
					this.Menus[i].Addition.Remarks = [];
				}
			}
		}
		$rootScope.dataSet = dataSet;
		return dataSet;
	}]
);

app.factory('cart', [
	'$rootScope',
	'$q',
	'$http',
	'dataSet',
	'$localStorage',
	'generateDataPromise',
	function ($rootScope, $q, $http, $dataSet, $localStorage, generateData) {
		var cart = {
			/* Cart Data */
			HeadCount: 1, // 总人数
			Price: 0, // 总价
			PriceInPoints: 0, // 通过积分支付的价格
			Invoice: null, // 发票抬头
			Desk: null,
			PayKind: null,
			OrderedMenus: [],

			TakeOut: {
				Name: null,
				Address: null,
				PhoneNumber: null
			},
			/* Cart Data */

			IsInitialized: false, // 是否初始化
			DiscountMethod: {
				Discount: 1,
				Name: ''
			},
			Customer: null,
			Ordered: 0, // 总共已选的份数

			Reset: function () {
				this.Ordered = 0;
				this.OriPrice = 0;
				this.Price = 0;
				this.OrderedMenus = [];
				$dataSet.Reset();
				delete $localStorage.cart;
			},
			Initialize: function (resolve) {
				if (this.IsInitialized) {
					if (resolve != null) resolve();
				} else {
					var _this = this;
					$rootScope.isLoading = true;
					generateData().then(function () {
						if (!angular.isUndefined($localStorage.cart)) {
							_this.LoadExistedCart($localStorage.cart);
						}
						$localStorage.cart = _this;
						$http.post('/Order/GetCurrentDesk').then(function (response) {
							_this.Desk = response.data;
						});
						if (resolve != null) resolve();
						_this.IsInitialized = true;
						$rootScope.isLoading = false;
					});
				}
			},

			LoadExistedCart: function (existedCart) {
				this.HeadCount = existedCart.HeadCount;
				this.Invoice = existedCart.Invoice;

				for (var i in existedCart.OrderedMenus) {
					for (var j in $dataSet.Menus) {
						if (existedCart.OrderedMenus[i].Id == $dataSet.Menus[j].Id) {
							var menu = existedCart.OrderedMenus[i];
							var count = menu.Addition.Ordered - $dataSet.Menus[j].MinOrderCount + 1;
							for (var k = 0; k < count; k++) {
								this.AddMenu($dataSet.Menus[j]);
							};
							for (var k in existedCart.OrderedMenus[i].Addition.Remarks) {
								for (var l in $dataSet.Menus[j].Remarks) {
									if (existedCart.OrderedMenus[i].Addition.Remarks[k].Id == $dataSet.Menus[j].Remarks[l].Id) {
										this.AddRemark($dataSet.Menus[j], $dataSet.Menus[j].Remarks[l], l);
										break;
									}
								}
							}
							break;
						}
					}
				}

				this.IsInitialized = true;
			},

			_menuPriceHandler: function (menu, ordered) {
				this.Price += menu.MenuPrice.Price * ordered;
			},
			AddMenu: function (menu) {
				var min = 1;
				if (menu.Addition.Ordered == 0) {
					min = menu.MinOrderCount;
				}
				menu.Addition.Ordered += min;
				this.Ordered += min;
				this._menuPriceHandler(menu, min);
				if (menu.Addition.Ordered == menu.MinOrderCount) {
					this.OrderedMenus.push(menu);
				}

				for (var i in $dataSet.MenuClasses) {
					for (var j in menu.MenuClasses) {
						if ($dataSet.MenuClasses[i].Id == menu.MenuClasses[j]) {
							$dataSet.MenuClasses[i].Addition.Ordered += min;
							break;
						}
					}
				}
			},
			RemoveMenu: function (menu) {
				var min = 1;
				if (menu.Addition.Ordered == menu.MinOrderCount) {
					min = menu.MinOrderCount;
				}
				menu.Addition.Ordered -= min;
				this.Ordered -= min;
				this._menuPriceHandler(menu, -min);
				if (menu.Addition.Ordered == 0) {
					for (var i = 0; i < this.OrderedMenus.length; i++) {
						if (angular.equals(menu, this.OrderedMenus[i])) {
							this.OrderedMenus.splice(i, 1);
							break;
						}
					}
				}

				for (var i in $dataSet.MenuClasses) {
					for (var j in menu.MenuClasses) {
						if ($dataSet.MenuClasses[i].Id == menu.MenuClasses[j]) {
							$dataSet.MenuClasses[i].Addition.Ordered -= min;
							break;
						}
					}
				}
			},
			AddRemark: function (menu, remark, index) {
				menu.Addition.Remarks.push(remark);
				menu.Remarks.splice(index, 1);
				this.Price += remark.Price;
			},
			RemoveRemark: function (menu, remark, index) {
				menu.Remarks.push(remark);
				menu.Addition.Remarks.splice(index, 1);
				this.Price -= remark.Price;
			},

			GetOrderedMenusWithFixed: function () {
				var filteredArr = [];
				for (var i in $dataSet.Menus) {
					if ($dataSet.Menus[i].IsFixed) {
						filteredArr.push($dataSet.Menus[i]);
					}
				}
				for (var i in this.OrderedMenus) {
					if (!this.OrderedMenus[i].IsFixed) {
						filteredArr.push(this.OrderedMenus[i]);
					}
				}
				return filteredArr
			},

			// 获得最优的整单折扣方案
			GetMinDiscountMethod: function () {
				var minDiscountMethod = {
					Discount: 1,
					Name: ''
				};
				if (!this.IsInitialized)
					return minDiscountMethod;

				if (this.PayKind != null) {
					if (this.PayKind.Discount < minDiscountMethod.Discount) {
						minDiscountMethod.Discount = this.PayKind.Discount;
						minDiscountMethod.Name = this.PayKind.Name + '折扣';
					}
				}
				var now = new Date();
				now = now.toTimeString();
				for (var i in $dataSet.DiscountMethods.TimeDiscounts) {
					var timeDiscount = $dataSet.DiscountMethods.TimeDiscounts[i];
					var from = new Date('1/1/1 ' + timeDiscount.From);
					var to = new Date('1/1/1 ' + timeDiscount.To);
					from = from.toTimeString();
					to = to.toTimeString();
					if (now >= from && now <= to) {
						if (timeDiscount.Discount < minDiscountMethod.Discount) {
							minDiscountMethod.Discount = timeDiscount.Discount;
							minDiscountMethod.Name = timeDiscount.Name;
						}
						break;
					}
				}

				if (this.Customer != null) {
					for (var i in $dataSet.DiscountMethods.VipDiscounts) {
						var vipDiscount = $dataSet.DiscountMethods.VipDiscounts[i];
						if (vipDiscount.Id == this.Customer.VipLevelId) {
							if (vipDiscount.Discount < minDiscountMethod.Discount) {
								minDiscountMethod.Discount = vipDiscount.Discount;
								minDiscountMethod.Name = vipDiscount.Name;
							}
							break;
						}
					}
				}
				this.DiscountMethod = minDiscountMethod;
				return minDiscountMethod;
			},

			// 获得最终要支付的金额
			GetPaymentPrice: function () {
				if (this.PayKind == null) {
					return null;
				}
				var price = 0;
				var discountMethod = this.GetMinDiscountMethod();
				for (var i in this.OrderedMenus) {
					var menuPrice = this.OrderedMenus[i].MenuPrice.Price * this.OrderedMenus[i].Addition.Ordered;
					if (!this.OrderedMenus[i].Addition.ExcludePayDiscount) { // 如果菜品在打支付总折的范围内，则打折
						menuPrice *= discountMethod.Discount;
					}
					price += menuPrice;

					for (var j in this.OrderedMenus[i].Addition.Remarks) {
						price += this.OrderedMenus[i].Addition.Remarks[j].Price;
					}
				}
				price = parseFloat(price.toFixed(2));
				if (this.PayKind.Type != 0) {
					this.PriceInPoints = 0;
				}
				// 如果用户抵扣的金额超过需要支付的金额
				if (this.PriceInPoints > price) {
					this.PriceInPoints = price;
				}
				// 总价减去积分抵扣的价格
				price -= this.PriceInPoints;
				return price;
			},
			CanSubmit: function () {
				return this.Price > 0 &&
					this.Price - this.PriceInPoints >= 0 && this.PriceInPoints >= 0
			},
			_getSendData: function () {
				var sendData = {
					HeadCount: this.HeadCount,
					Price: this.GetPaymentPrice() + this.PriceInPoints,
					PriceInPoints: this.PriceInPoints,
					Invoice: this.Invoice,

					DeskId: this.Desk.Id,
					PayKindId: this.PayKind.Id,

					OrderedMenus: [],

					TakeOut: this.TakeOut
				}

				for (var i in this.OrderedMenus) {
					var orderedMenu = {
						Id: this.OrderedMenus[i].Id,
						Ordered: this.OrderedMenus[i].Addition.Ordered,
						Remarks: []
					}
					for (var j in this.OrderedMenus[i].Addition.Remarks) {
						orderedMenu.Remarks.push(this.OrderedMenus[i].Addition.Remarks[j].Id);
					}

					sendData.OrderedMenus.push(orderedMenu);
				}
				return sendData;
			},
			Submit: function () {
				var _this = this;
				return $q(function (resolve, reject) {
					var sendData = _this._getSendData();
					$http.post('/Payment/Pay', sendData).then(function (response) {
						resolve(response.data);
					}, function () {
						reject();
					});
				});
			}
		}
		$rootScope.cart = cart;
		return cart;
	}]
).factory('menuFilter', [
	'$rootScope',
	'$filter',
	'cart',
	'dataSet',
	function ($rootScope, $filter, $cart, $dataSet) {

		var mode = {
			search: 0,
			normal: 1,
			rank: 2,
			onSale: 3,
			result: 4
		}
		var currentMode = mode.normal;

		var menuFilter = {
			FilteredMenus: [],
			SearchText: '',
			_activeMenuClass: null,

			IsRankMode: function () {
				return currentMode == mode.rank;
			},
			IsSearchMode: function () {
				return currentMode == mode.search;
			},
			IsOnSaleMode: function () {
				return currentMode == mode.onSale;
			},
			IsResultMode: function () {
				return currentMode == mode.result;
			},
			_changeMode: function (m) {
				currentMode = m;
				if (m != mode.search) {
					this.SearchText = '';
				}
				if (m != mode.normal && this._activeMenuClass != null) {
					this._activeMenuClass.Addition.IsSelected = false;
				}
			},
			IntoRankMode: function () {
				this._changeMode(mode.rank);
				this._filterMenu();
			},
			IntoOnSaleMode: function () {
				this._changeMode(mode.onSale);
				this._filterMenu();
			},
			IntoResultMode: function () {
				this._changeMode(mode.result);
				this._filterMenu();
			},
			ToggleSelected: function (menuClass) {
				this._changeMode(mode.normal);

				if (this._activeMenuClass != null) {
					this._activeMenuClass.Addition.IsSelected = false;
				}

				menuClass.Addition.IsSelected = true;
				this._activeMenuClass = menuClass;
				this._filterMenu();
			},
			ChangeSearchText: function () {
				if (this.SearchText == '') {
					this._changeMode(mode.rank);
				} else {
					this._changeMode(mode.search);
				}
				this._filterMenu();
			},
			_filterMenu: function () {
				var filteredArr = [];

				switch (currentMode) {
					case mode.normal:
						filteredArr = $filter('filter')($dataSet.Menus, {
							MenuClasses: this._activeMenuClass.Id
						}, true);
						filteredArr = $filter('orderBy')(filteredArr, '-Ordered');
						break;
					case mode.rank:
						filteredArr = $filter('orderBy')($dataSet.Menus, '-Ordered');
						filteredArr = $filter('limitTo')(filteredArr, 15);
						break;
					case mode.search:
						filteredArr = $filter('filter')($dataSet.Menus, {
							Code: this.SearchText
						});
						filteredArr = $filter('limitTo')(filteredArr, 15);
						break;
					case mode.onSale:
						for (var i in $dataSet.Menus) {
							if ($dataSet.Menus[i].Addition.IsOnSale) {
								filteredArr.push($dataSet.Menus[i]);
							}
						}
						break;
				}

				this.FilteredMenus = filteredArr;
			}
		}

		$rootScope.menuFilter = menuFilter;
		return menuFilter;
	}]
);

app.factory('dineToCart', [
	'cart',
	function ($cart) {
		return function (dine) {
			var cart = {
				HeadCount: dine.HeadCount,
				Invoice: dine.Invoice,
				OrderedMenus: [],
			}
			for (var i = 0; i < dine.DineMenus.length; i++) {
				var detail = dine.DineMenus[i];
				cart.OrderedMenus.push({
					Id: detail.Menu.Id,
					Addition: {
						Ordered: detail.Count,
						Remarks: detail.Remarks
					}
				});
			}
			$cart.Reset();
			$cart.LoadExistedCart(cart);
		}
	}
]);