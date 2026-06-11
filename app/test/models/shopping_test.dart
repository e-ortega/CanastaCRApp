import 'package:flutter_test/flutter_test.dart';
import 'package:canasta_cr/core/models/shopping.dart';

Map<String, dynamic> _itemJson({bool isPurchased = false}) => {
      'id': 'item-1',
      'productId': 'prod-1',
      'productName': 'Arroz',
      'productImageUrl': null,
      'quantity': 2,
      'unit': 'Unit',
      'isPurchased': isPurchased,
    };

Map<String, dynamic> _listJson(List<Map<String, dynamic>> items) => {
      'id': 'list-1',
      'name': 'Lista del mercado',
      'createdAt': '2026-06-01T10:00:00Z',
      'items': items,
    };

void main() {
  group('ShoppingListItem.fromJson', () {
    test('parses all fields', () {
      final i = ShoppingListItem.fromJson(_itemJson());
      expect(i.id, 'item-1');
      expect(i.productId, 'prod-1');
      expect(i.productName, 'Arroz');
      expect(i.quantity, 2.0);
      expect(i.unit, 'Unit');
      expect(i.isPurchased, false);
    });

    test('coerces int quantity to double', () {
      final i = ShoppingListItem.fromJson(_itemJson());
      expect(i.quantity, isA<double>());
    });
  });

  group('ShoppingList.fromJson', () {
    test('parses list with items', () {
      final l = ShoppingList.fromJson(_listJson([_itemJson(), _itemJson(isPurchased: true)]));
      expect(l.id, 'list-1');
      expect(l.name, 'Lista del mercado');
      expect(l.items, hasLength(2));
    });

    test('parses empty items list', () {
      final l = ShoppingList.fromJson(_listJson([]));
      expect(l.items, isEmpty);
    });
  });

  group('ShoppingList.pendingCount', () {
    test('counts only unpurchased items', () {
      final l = ShoppingList.fromJson(_listJson([
        _itemJson(isPurchased: false),
        _itemJson(isPurchased: true),
        _itemJson(isPurchased: false),
      ]));
      expect(l.pendingCount, 2);
    });

    test('returns 0 when all items purchased', () {
      final l = ShoppingList.fromJson(_listJson([
        _itemJson(isPurchased: true),
        _itemJson(isPurchased: true),
      ]));
      expect(l.pendingCount, 0);
    });

    test('returns full count when no items purchased', () {
      final l = ShoppingList.fromJson(_listJson([
        _itemJson(),
        _itemJson(),
        _itemJson(),
      ]));
      expect(l.pendingCount, 3);
    });
  });

  group('OptimizationResult.fromJson', () {
    final json = {
      'shoppingListId': 'list-1',
      'totalEstimatedCost': 1700,
      'totalIfSingleStore': 2050,
      'totalSavings': 350,
      'savingsPercent': 17.07,
      'storeGroups': [
        {
          'storeId': 'store-1',
          'storeName': 'MaxiPalí',
          'chain': 'MaxiPali',
          'groupTotal': 1700,
          'items': [
            {
              'productId': 'prod-1',
              'productName': 'Arroz',
              'price': 900,
              'quantity': 1,
              'unit': 'Unit',
              'lineTotal': 900,
              'savingsVsHighest': 100,
            },
            {
              'productId': 'prod-2',
              'productName': 'Leche',
              'price': 800,
              'quantity': 1,
              'unit': 'Unit',
              'lineTotal': 800,
              'savingsVsHighest': 200,
            },
          ],
        }
      ],
    };

    test('parses cost and savings fields', () {
      final r = OptimizationResult.fromJson(json);
      expect(r.totalEstimatedCost, 1700.0);
      expect(r.totalIfSingleStore, 2050.0);
      expect(r.totalSavings, 350.0);
      expect(r.savingsPercent, closeTo(17.07, 0.01));
    });

    test('parses store groups', () {
      final r = OptimizationResult.fromJson(json);
      expect(r.storeGroups, hasLength(1));
      expect(r.storeGroups[0].storeName, 'MaxiPalí');
      expect(r.storeGroups[0].groupTotal, 1700.0);
    });

    test('parses items within store group', () {
      final r = OptimizationResult.fromJson(json);
      final items = r.storeGroups[0].items;
      expect(items, hasLength(2));
      expect(items[0].productName, 'Arroz');
      expect(items[0].lineTotal, 900.0);
      expect(items[1].savingsVsHighest, 200.0);
    });

    test('empty store groups when all items purchased', () {
      final r = OptimizationResult.fromJson({
        'shoppingListId': 'list-1',
        'totalEstimatedCost': 0,
        'totalIfSingleStore': 0,
        'totalSavings': 0,
        'savingsPercent': 0,
        'storeGroups': [],
      });
      expect(r.storeGroups, isEmpty);
      expect(r.totalSavings, 0.0);
    });
  });
}
