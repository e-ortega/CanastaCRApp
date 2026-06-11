import 'package:flutter_test/flutter_test.dart';
import 'package:canasta_cr/core/models/pantry.dart';

Map<String, dynamic> _pantryJson({
  double quantity = 3.0,
  double minThreshold = 2.0,
  bool isRunningLow = false,
  String? lastPurchasedAt,
}) =>
    {
      'id': 'pantry-1',
      'productId': 'prod-1',
      'productName': 'Arroz Tío Pelón 1kg',
      'productImageUrl': null,
      'quantity': quantity,
      'unit': 'Unit',
      'minThreshold': minThreshold,
      'isRunningLow': isRunningLow,
      'lastPurchasedAt': lastPurchasedAt,
    };

void main() {
  group('PantryItem.fromJson', () {
    test('parses all fields', () {
      final p = PantryItem.fromJson(_pantryJson(lastPurchasedAt: '2026-05-15T00:00:00Z'));

      expect(p.id, 'pantry-1');
      expect(p.productId, 'prod-1');
      expect(p.productName, 'Arroz Tío Pelón 1kg');
      expect(p.quantity, 3.0);
      expect(p.unit, 'Unit');
      expect(p.minThreshold, 2.0);
      expect(p.isRunningLow, false);
      expect(p.lastPurchasedAt, DateTime.parse('2026-05-15T00:00:00Z'));
    });

    test('handles null lastPurchasedAt', () {
      final p = PantryItem.fromJson(_pantryJson());
      expect(p.lastPurchasedAt, isNull);
    });

    test('coerces int quantity and threshold to double', () {
      final p = PantryItem.fromJson({
        'id': 'p',
        'productId': 'x',
        'productName': 'X',
        'productImageUrl': null,
        'quantity': 2,         // int
        'unit': 'Unit',
        'minThreshold': 1,     // int
        'isRunningLow': false,
        'lastPurchasedAt': null,
      });
      expect(p.quantity, isA<double>());
      expect(p.minThreshold, isA<double>());
    });

    test('reflects running low flag from API', () {
      final p = PantryItem.fromJson(_pantryJson(quantity: 0.5, minThreshold: 2.0, isRunningLow: true));
      expect(p.isRunningLow, true);
    });
  });

  group('PantryItem.stockPercent', () {
    test('returns ratio of quantity to threshold', () {
      final p = PantryItem.fromJson(_pantryJson(quantity: 1.0, minThreshold: 2.0));
      expect(p.stockPercent, 0.5);
    });

    test('clamps to 1.0 when quantity exceeds threshold', () {
      final p = PantryItem.fromJson(_pantryJson(quantity: 5.0, minThreshold: 2.0));
      expect(p.stockPercent, 1.0);
    });

    test('returns 0.0 when quantity is zero', () {
      final p = PantryItem.fromJson(_pantryJson(quantity: 0.0, minThreshold: 2.0));
      expect(p.stockPercent, 0.0);
    });

    test('returns 1.0 when threshold is zero (prevents division by zero)', () {
      final p = PantryItem.fromJson(_pantryJson(quantity: 3.0, minThreshold: 0.0));
      expect(p.stockPercent, 1.0);
    });
  });
}
