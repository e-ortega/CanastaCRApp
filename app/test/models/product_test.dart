import 'package:flutter_test/flutter_test.dart';
import 'package:canasta_cr/core/models/product.dart';

void main() {
  group('Product.fromJson', () {
    test('parses all fields', () {
      final p = Product.fromJson({
        'id': 'abc-123',
        'barcode': '7400001234567',
        'name': 'Arroz Tío Pelón 1kg',
        'brand': 'Tío Pelón',
        'category': 'Granos',
        'imageUrl': 'https://example.com/rice.jpg',
        'description': 'Arroz de grano largo',
      });

      expect(p.id, 'abc-123');
      expect(p.barcode, '7400001234567');
      expect(p.name, 'Arroz Tío Pelón 1kg');
      expect(p.brand, 'Tío Pelón');
      expect(p.category, 'Granos');
      expect(p.imageUrl, 'https://example.com/rice.jpg');
      expect(p.description, 'Arroz de grano largo');
    });

    test('handles null optional fields', () {
      final p = Product.fromJson({
        'id': 'abc-123',
        'barcode': null,
        'name': 'Leche',
        'brand': null,
        'category': null,
        'imageUrl': null,
        'description': null,
      });

      expect(p.barcode, isNull);
      expect(p.brand, isNull);
      expect(p.category, isNull);
      expect(p.imageUrl, isNull);
      expect(p.description, isNull);
    });
  });

  group('ProductSearchResult.fromJson', () {
    test('parses numeric price fields from int and double', () {
      final r = ProductSearchResult.fromJson({
        'id': 'xyz',
        'barcode': null,
        'name': 'Leche Dos Pinos 1L',
        'brand': 'Dos Pinos',
        'category': 'Lácteos',
        'imageUrl': null,
        'lowestPrice': 975,       // int from JSON
        'lowestPriceStore': 'MaxiPalí',
      });

      expect(r.lowestPrice, 975.0);
      expect(r.lowestPrice, isA<double>());
      expect(r.lowestPriceStore, 'MaxiPalí');
    });

    test('handles null price when no prices have been reported', () {
      final r = ProductSearchResult.fromJson({
        'id': 'xyz',
        'barcode': null,
        'name': 'Nuevo Producto',
        'brand': null,
        'category': null,
        'imageUrl': null,
        'lowestPrice': null,
        'lowestPriceStore': null,
      });

      expect(r.lowestPrice, isNull);
      expect(r.lowestPriceStore, isNull);
    });
  });
}
