app.controller('MenuCtrl', [
	'$scope',
	'$rootScope',
	'$uibModal',
	'cart',
	'menuFilter',
	function ($scope, $rootScope, $modal, $cart, $menuFilter) {
		$scope.showMenuModal = function (menu) {
			$modal.open({
				templateUrl: 'menuModal.html',
				controller: 'MenuModalCtrl',
				resolve: {
					menu: function () {
						return menu;
					}
				}
			});
		}

		$scope.showSetMealsModal = function (menu) {
			$modal.open({
				templateUrl: 'setMealsModal.html',
				controller: 'SetMealsCtrl',
				resolve: {
					menu: function () {
						return menu;
					}
				}
			});
		}
		$cart.Initialize(function () {
			$menuFilter.IntoRankMode();
		});
	}
]);

app.controller('MenuModalCtrl', [
	'$scope',
	'$uibModalInstance',
	'menu',
	function ($scope, $modalInstance, $menu) {
		$scope.menu = $menu;
		$scope.close = function () {
			$modalInstance.dismiss();
		};
	}
]);

app.controller('SetMealsCtrl', [
	'$scope',
	'$uibModalInstance',
	'menu',
	function ($scope, $modalInstance, $menu) {
		$scope.menu = $menu;
		$scope.close = function () {
			$modalInstance.dismiss();
		};
	}
]);

app.controller('CartCtrl', [
	'$scope',
	'$rootScope',
	'$uibModal',
	'cart',
	function ($scope, $rootScope, $modal, $cart) {
		$cart.Initialize();
	}
]);


app.controller('PaymentCtrl', [
	'$scope',
	'dataSet',
	'cart',
	'$http',
	'$location',
	'$window',
	function ($scope, $dataSet, $cart, $http, $location, $window) {
		$cart.Initialize();

		var headCountsAll = [];
		for (var i = 1; i < 50; i++) {
			headCountsAll.push(i);
		}
		$scope.headCountsAll = headCountsAll;

		$http.post('/Account/IsAuthenticated').then(function (response) {
			if (response.data.Succeeded) {
				$cart.Customer = response.data.Data;
				if ($cart.Customer != null) {
					$cart.TakeOut.PhoneNumber = $cart.Customer.PhoneNumber;
				}
			}
		});
		$http.post('/Order/GetUserAddresses').then(function (response) {
			$scope.userAddresses = response.data;
			if ($scope.userAddresses.length > 0) {
				$cart.TakeOut.Address = $scope.userAddresses[0];
			}
		});

		$scope.canSubmit = function () {
			var result = $cart.CanSubmit() && !$scope.isSubmitting && $cart.PayKind != null;
			if ($cart.Customer != null && $dataSet.Hotel.PointsRatio > 0) {
				result = result && $cart.Customer.Points / $dataSet.Hotel.PointsRatio >= $cart.PriceInPoints;
			}
			return result;
		}

		$scope.isSubmitting = false;

		$scope.pay = function () {
			if ($scope.isSubmitting) return;
			$scope.isSubmitting = true;

			$cart.Submit().then(function (data) {
				if (data.Succeeded) {
					$cart.Reset();
					$window.location.href = data.RedirectUrl;
				} else {
					toastr.error(data.ErrorMessage);
				}
				$scope.isSubmitting = false;
			}, function () {
				toastr.error('网络连接失败，请稍候重试');
				$scope.isSubmitting = false;
			});
		}
	}
]);


app.controller('HistoryCtrl', [
	'$rootScope',
	'$scope',
	'$http',
	'$window',
	'$location',

	'cart',
	'dineToCart',
	function ($rootScope, $scope, $http, $window, $location, $cart, $dineToCart) {
		$cart.Initialize();
		$rootScope.isLoading = true;
		$http.post('/Order/GetHistoryDines').then(function (response) {
			$scope.dines = response.data;
			$rootScope.isLoading = false;
		});

		$scope.tryAgain = function (dine) {
			$dineToCart(dine);
			$location.path('/Cart');
		}
		$scope.payAgain = function (dine) {
			$http.post('/Payment/PayAgain', { DineId: dine.Id }).then(function (response) {
				$window.location.href = response.data.RedirectUrl;
			});
		}
	}
]);