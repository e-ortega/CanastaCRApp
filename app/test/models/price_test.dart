import 'package:flutter_test/flutter_test.dart';
import 'package:canasta_cr/core/models/price.dart';

void main() {
  group('StorePrice.fromJson', () {
    test('parses all fields', () {
      final sp = StorePrice.fromJson({
        'storeId': 'store-1',
        'storeName': 'AutoMercado Escazú',
        'chain': 'AutoMercado',
        'price': 1250,
        'currency': 'CRC',
        'reportedAt': '2026-06-01T10:00:00Z',
        'isExpired': false,
      });

      expect(sp.storeId, 'store-1');
      expect(sp.storeName, 'AutoMercado Escazú');
      expect(sp.chain, 'AutoMercado');
      expect(sp.price, 1250.0);
      expect(sp.currency, 'CRC');
      expect(sp.isExpired, false);
      expect(sp.reportedAt, DateTime.parse('2026-06-01T10:00:00Z'));
    });

    test('coerces int price to double', () {
      final sp = StorePrice.fromJson({
        'storeId': 's',
        'storeName': 'X',
        'chain': 'MaxiPali',
        'price': 975,
        'currency': 'CRC',
        'reportedAt': '2026-01-01T00:00:00Z',
        'isExpired': false,
      });

      expect(sp.price, isA<double>());
      expect(sp.price, 975.0);
    });

    test('marks expired prices', () {
      final sp = StorePrice.fromJson({
        'storeId': 's',
        'storeName': 'X',
        'chain': 'MaxiPali',
        'price': 500,
        'currency': 'CRC',
        'reportedAt': '2025-01-01T00:00:00Z',
        'isExpired': true,
      });

      expect(sp.isExpired, true);
    });
  });

  group('PriceComparison.fromJson', () {
    final json = {
      'productId': 'prod-1',
      'productName': 'Leche Dos Pinos 1L',
      'productImageUrl': null,
      'prices': [
        {
          'storeId': 'store-1',
          'storeName': 'AutoMercado',
          'chain': 'AutoMercado',
          'price': 1250,
          'currency': 'CRC',
          'reportedAt': '2026-06-01T10:00:00Z',
          'isExpired': false,
        },
        {
          'storeId': 'store-2',
          'storeName': 'MaxiPalí',
          'chain': 'MaxiPali',
          'price': 975,
          'currency': 'CRC',
          'reportedAt': '2026-06-02T08:00:00Z',
          'isExpired': false,
        },
      ],
      'lowestPrice': 975,
      'highestPrice': 1250,
      'savingsAmount': 275,
      'savingsPercent': 22.0,
    };

    test('parses product fields', () {
      final c = PriceComparison.fromJson(json);
      expect(c.productId, 'prod-1');
      expect(c.productName, 'Leche Dos Pinos 1L');
      expect(c.productImageUrl, isNull);
    });

    test('parses prices list', () {
      final c = PriceComparison.fromJson(json);
      expect(c.prices, hasLength(2));
      expect(c.prices[0].storeName, 'AutoMercado');
      expect(c.prices[1].price, 975.0);
    });

    test('parses savings fields', () {
      final c = PriceComparison.fromJson(json);
      expect(c.lowestPrice, 975.0);
      expect(c.highestPrice, 1250.0);
      expect(c.savingsAmount, 275.0);
      expect(c.savingsPercent, 22.0);
    });

    test('handles null savings when only one store', () {
      final single = Map<String, dynamic>.from(json)
        ..['prices'] = [(json['prices'] as List).first]
        ..['lowestPrice'] = null
        ..['highestPrice'] = null
        ..['savingsAmount'] = null
        ..['savingsPercent'] = null;

      final c = PriceComparison.fromJson(single);
      expect(c.lowestPrice, isNull);
      expect(c.savingsAmount, isNull);
    });
  });
}
