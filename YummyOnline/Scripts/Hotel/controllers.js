app.controller('HotelCtrl', [
	'$scope',
	'$http',
	'layout',
	function ($scope, $http, $layout) {
		$layout.Set('饭店管理', '');

		function refresh() {
			$http.post('/Hotel/GetHotels').then(function (response) {
				$scope.hotels = response.data;
				for (var i in $scope.hotels) {
					var h = $scope.hotels[i];
					h.NewData = {
						CssThemePath: h.CssThemePath,
						ConnectionString: h.ConnectionString,
						AdminConnectionString: h.AdminConnectionString,
						Usable: h.Usable
					}
					h.IsReadyForConfirm = false;
					if (h.ConnectionString == null && !h.Usable) {
						h.IsReadyForConfirm = true;
						h.DatabaseName = 'YummyOnlineHotel' + h.Id;
					}
				}
			})
		}

		refresh();

		$scope.update = function (hotel) {
			$http.post('/Hotel/UpdateHotel', {
				Id: hotel.Id,
				CssThemePath: hotel.NewData.CssThemePath,
				ConnectionString: hotel.NewData.ConnectionString,
				AdminConnectionString: hotel.NewData.AdminConnectionString,
				Usable: hotel.NewData.Usable
			}).then(function (response) {
				if (response.data.Succeeded) {
					toastr.success('修改成功');
					refresh();
				} else {
					toastr.error('修改失败');
					refresh();
				}
			})
		}
		$scope.create = function (hotel) {
			$http.post('/Hotel/CreateHotel', {
				HotelId: hotel.Id,
				DatabaseName: hotel.DatabaseName,
			}).then(function (response) {
				if (response.data.Succeeded) {
					toastr.success('创建成功');
					refresh();
				} else {
					toastr.error(response.data.ErrorMessage);
				}
			})
		}
	}
]).controller('DineCtrl', [
	'$scope',
	'$http',
	'layout',
	function ($scope, $http, $layout) {
		$layout.Set('订单查询', '');

		$scope.dines = [];
		$http.post('/Hotel/GetHotelNames').then(function (response) {
			$scope.hotels = response.data;
			$scope.hotelId = $scope.hotels[0].Id;
		});

		$scope.dateTime = new Date();
		$scope.refreshDineIds = function () {
			$http.post('/Hotel/GetAllDineIds', {
				HotelId: $scope.hotelId,
				DateTime: $scope.dateTime
			}).then(function (response) {
				$scope.allDineIds = response.data;
			});
		}

		$scope.dineIds = '';
		$scope.addDineId = function (dineId) {
			$scope.dineIds += dineId + ' ';
		}
		$scope.addAllDineIds = function () {
			for (var i in $scope.allDineIds) {
				$scope.dineIds += $scope.allDineIds[i] + ' ';
			}
		}

		$scope.search = function () {
			$scope.isLoading = true;
			var dineIds = $scope.dineIds.split(/\s+/);
			$http.post('/Hotel/GetDines', {
				HotelId: $scope.hotelId,
				DineIds: dineIds
			}).then(function (response) {
				$scope.dines = response.data;
				$scope.isLoading = false;
			}, function (response) {
				$scope.isLoading = false;
			});
		}

		$scope.weixinNotify = function (dine) {
			$http.post('/Hotel/WeixinNotify', {
				HotelId: $scope.hotelId,
				DineId: dine.Id
			}).then(function (response) {
				if (response.data.Succeeded) {
					$scope.search();
				} else {
					toastr.error('支付失败');
				}
			});
		}
	}
]).controller('ArticleCtrl', [
	'$scope',
	'$http',
	'$uibModal',
	'layout',
	function ($scope, $http, $modal, $layout) {
		$layout.Set('文章管理', '');

		$scope.dines = [];
		$http.post('/Hotel/GetHotelNames').then(function (response) {
			$scope.hotels = response.data;
			$scope.hotels.push({
				Id: null,
				Name: 'YummyOnline'
			});
			$scope.hotelId = $scope.hotels[0].Id;
			$scope.refreshArticles($scope.hotelId);
		});

		$scope.refreshArticles = function (hotelId) {
			$http.post('/Hotel/GetArticles', {
				HotelId: $scope.hotelId,
			}).then(function (response) {
				$scope.articles = response.data;
			});
		}

		$scope.newArticle = {};
		$scope.newArticle.PicturePath = 'http://static.yummyonline.net/';

		var editor;
		$(document).ready(function () {
			editor = KindEditor.create('#article-body', {
				resizeType: 1,
				allowPreviewEmoticons: false,
				allowImageUpload: false,
				themesPath: '/content/css/lib/',
				items: [
					'formatblock', 'fontsize', 'forecolor', 'hilitecolor', '|',
					'justifyleft', 'justifycenter', 'justifyright', 'justifyfull', '|',
					'insertorderedlist', 'insertunorderedlist', 'indent', 'outdent', 'subscript', 'superscript', '|',
					'bold', 'italic', 'underline', 'strikethrough', 'lineheight', 'quickformat', 'removeformat'
				],
			});
		});

		$scope.showImageModal = function () {
			$modal.open({
				templateUrl: 'imageModal.html',
				controller: 'ImageModalCtrl',
				resolve: {
					editor: function () {
						return editor;
					}
				}
			});
		}
		$scope.showLinkModal = function () {
			$modal.open({
				templateUrl: 'linkModal.html',
				controller: 'LinkModalCtrl',
				resolve: {
					editor: function () {
						return editor;
					}
				}
			});
		}
		$scope.showArticleBodyModal = function (article) {
			$modal.open({
				templateUrl: 'articleBodyModal.html',
				controller: 'ArticleBodyModalCtrl',
				resolve: {
					article: function () {
						return article;
					}
				}
			});
		}

		$scope.add = function () {
			$http.post('/Hotel/AddArticle', {
				Title: $scope.newArticle.Title,
				PicturePath: $scope.newArticle.PicturePath,
				Description: $scope.newArticle.Description,
				Body: editor.html(),
				HotelId: $scope.hotelId
			}).then(function (response) {
				if (response.data.Succeeded) {
					$scope.refreshArticles($scope.hotelId);
				} else {
					toastr.error(response.data.ErrorMessage);
				}
			});
		}
		$scope.remove = function (article) {
			$http.post('/Hotel/RemoveArticle', {
				Id: article.Id
			}).then(function (response) {
				if (response.data.Succeeded) {
					$scope.refreshArticles($scope.hotelId);
				} else {
					toastr.error(response.data.ErrorMessage);
				}
			});
		}
	}
]);

app.controller('ImageModalCtrl', [
	'$scope',
	'$uibModalInstance',
	'editor',
	function ($scope, $modalInstance, $editor) {
		$scope.newImagePath = 'http://static.yummyonline.net/'
		$scope.cancel = function () {
			$modalInstance.dismiss();
		};
		$scope.ok = function () {
			$editor.exec('insertimage', $scope.newImagePath);
			$modalInstance.dismiss();
		};
	}
]);

app.controller('LinkModalCtrl', [
	'$scope',
	'$uibModalInstance',
	'editor',
	function ($scope, $modalInstance, $editor) {
		$scope.newLinkPath = 'http://static.yummyonline.net/'
		$scope.cancel = function () {
			$modalInstance.dismiss();
		};
		$scope.ok = function () {
			$editor.exec('createlink', $scope.newLinkPath, '_blank');
			$modalInstance.dismiss();
		};
	}
]);

app.controller('ArticleBodyModalCtrl', [
	'$scope',
	'$uibModalInstance',
	'article',
	function ($scope, $modalInstance, $article) {
		$scope.article = $article;

		$scope.close = function () {
			$modalInstance.dismiss();
		};
	}
]);