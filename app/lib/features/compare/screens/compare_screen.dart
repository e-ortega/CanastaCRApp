import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/price.dart';

class CompareScreen extends StatefulWidget {
  final String productId;
  const CompareScreen({super.key, required this.productId});

  @override
  State<CompareScreen> createState() => _CompareScreenState();
}

class _CompareScreenState extends State<CompareScreen> {
  final _api = ApiClient();
  PriceComparison? _data;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final json = await _api.get('/products/${widget.productId}/prices');
      setState(() => _data = PriceComparison.fromJson(json));
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_data?.productName ?? 'Comparar precios'),
        actions: [
          if (_data != null)
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              tooltip: 'Editar nombre del producto',
              onPressed: () => _showEditProductName(context),
            ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _data == null
              ? const Center(child: Text('No se encontraron precios'))
              : _buildContent(),
    );
  }

  Widget _buildContent() {
    final d = _data!;
    final active = d.prices.where((p) => !p.isExpired).toList();
    final highest = active.isEmpty ? 1.0 : active.map((p) => p.price).reduce((a, b) => a > b ? a : b);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (d.productName == unnamedProductPlaceholder)
          Container(
            padding: const EdgeInsets.all(14),
            margin: const EdgeInsets.only(bottom: 16),
            decoration: BoxDecoration(
              color: const Color(0xFFFAEEDA),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Row(
              children: [
                const Icon(Icons.warning_amber_rounded, color: Color(0xFF854F0B)),
                const SizedBox(width: 10),
                const Expanded(
                  child: Text(
                    'No se reconoció este producto. Toca el lápiz arriba para ponerle un nombre antes de reportar un precio.',
                    style: TextStyle(fontSize: 13, color: Color(0xFF633806)),
                  ),
                ),
              ],
            ),
          ),
        if (d.savingsAmount != null && d.savingsAmount! > 0)
          Container(
            padding: const EdgeInsets.all(14),
            margin: const EdgeInsets.only(bottom: 16),
            decoration: BoxDecoration(
              color: const Color(0xFFE1F5EE),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Row(
              children: [
                const Icon(Icons.savings_outlined, color: Color(0xFF1D9E75)),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Ahorra hasta ₡${d.savingsAmount!.toStringAsFixed(0)} por unidad',
                        style: const TextStyle(fontWeight: FontWeight.w600, color: Color(0xFF085041)),
                      ),
                      Text(
                        '${d.savingsPercent!.toStringAsFixed(1)}% diferencia entre tiendas',
                        style: const TextStyle(fontSize: 12, color: Color(0xFF0F6E56)),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        const Text('PRECIOS ACTUALES',
            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)),
        const SizedBox(height: 8),
        if (d.prices.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 24),
            child: Column(
              children: [
                Icon(Icons.price_check, size: 48, color: Colors.grey[400]),
                const SizedBox(height: 8),
                Text(
                  'Aún no hay precios reportados.\nSé el primero en reportar un precio.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Colors.grey[500]),
                ),
              ],
            ),
          )
        else
          ...d.prices.map((p) => _buildPriceRow(p, highest)),
        const SizedBox(height: 12),
        Text(
          'Precios reportados por usuarios. Vencen a los 90 días.',
          style: TextStyle(fontSize: 11, color: Colors.grey[500]),
        ),
        const SizedBox(height: 16),
        OutlinedButton.icon(
          onPressed: () => _showReportPrice(context),
          icon: const Icon(Icons.add_chart),
          label: const Text('Reportar precio'),
          style: OutlinedButton.styleFrom(
            foregroundColor: const Color(0xFF1D9E75),
            side: const BorderSide(color: Color(0xFF1D9E75)),
          ),
        ),
        const SizedBox(height: 8),
        ElevatedButton.icon(
          onPressed: () => _showAddToList(context),
          icon: const Icon(Icons.add_shopping_cart),
          label: const Text('Agregar a mi lista'),
        ),
      ],
    );
  }

  Future<void> _showEditProductName(BuildContext context) async {
    final updated = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => _EditProductNameSheet(
        api: _api,
        productId: widget.productId,
        currentName: _data?.productName ?? '',
      ),
    );
    if (updated == true) {
      setState(() => _loading = true);
      _load();
    }
  }

  Future<void> _showReportPrice(BuildContext context) async {
    final submitted = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => _ReportPriceSheet(
        api: _api,
        productId: widget.productId,
        productName: _data?.productName ?? '',
      ),
    );
    if (submitted == true) {
      setState(() => _loading = true);
      _load();
    }
  }

  Future<void> _showAddToList(BuildContext context) async {
    final lists = await _api.getList('/shopping-lists');
    if (!context.mounted) return;
    if (lists.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Primero crea una lista de compras')),
      );
      return;
    }
    final selected = await showModalBottomSheet<Map<String, dynamic>>(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (_) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(width: 36, height: 4, decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2))),
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text('Agregar a lista', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
          ),
          ...lists.map((l) => ListTile(
                leading: const Icon(Icons.shopping_cart_outlined, color: Color(0xFF1D9E75)),
                title: Text(l['name']),
                onTap: () => Navigator.pop(context, l),
              )),
          const SizedBox(height: 16),
        ],
      ),
    );
    if (selected == null || !context.mounted) return;
    await _api.post('/shopping-lists/${selected['id']}/items', {
      'productId': widget.productId,
      'quantity': 1,
      'unit': 0,
    });
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Agregado a "${selected['name']}"'),
          backgroundColor: const Color(0xFF1D9E75),
        ),
      );
    }
  }

  Widget _buildPriceRow(StorePrice p, double highest) {
    final isLowest = _data!.lowestPrice != null && p.price == _data!.lowestPrice;
    final isHighest = _data!.highestPrice != null && p.price == _data!.highestPrice;
    final barWidth = highest > 0 ? p.price / highest : 0.0;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(child: Text(p.storeName, style: const TextStyle(fontSize: 14))),
              if (isLowest)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                      color: const Color(0xFFE1F5EE), borderRadius: BorderRadius.circular(20)),
                  child: const Text('Más barato',
                      style: TextStyle(fontSize: 11, color: Color(0xFF085041), fontWeight: FontWeight.w600)),
                ),
              if (isHighest)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                      color: const Color(0xFFFAECE7), borderRadius: BorderRadius.circular(20)),
                  child: const Text('Más caro',
                      style: TextStyle(fontSize: 11, color: Color(0xFF4A1B0C), fontWeight: FontWeight.w600)),
                ),
              const SizedBox(width: 8),
              Text(
                '₡${p.price.toStringAsFixed(0)}',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: isLowest ? const Color(0xFF085041) : null,
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          ClipRRect(
            borderRadius: BorderRadius.circular(3),
            child: LinearProgressIndicator(
              value: barWidth,
              minHeight: 6,
              backgroundColor: Colors.grey[200],
              color: isLowest
                  ? const Color(0xFF1D9E75)
                  : isHighest
                      ? const Color(0xFFF0997B)
                      : const Color(0xFF9FE1CB),
            ),
          ),
          if (p.isExpired)
            const Align(
              alignment: Alignment.centerLeft,
              child: Text('Precio vencido', style: TextStyle(fontSize: 11, color: Colors.orange)),
            ),
        ],
      ),
    );
  }
}

class _ReportPriceSheet extends StatefulWidget {
  final ApiClient api;
  final String productId;
  final String productName;

  const _ReportPriceSheet({
    required this.api,
    required this.productId,
    required this.productName,
  });

  @override
  State<_ReportPriceSheet> createState() => _ReportPriceSheetState();
}

class _ReportPriceSheetState extends State<_ReportPriceSheet> {
  final _priceCtrl = TextEditingController();
  List<Map<String, dynamic>> _stores = [];
  Map<String, dynamic>? _selectedStore;
  bool _loadingStores = true;
  bool _submitting = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadStores();
  }

  Future<void> _loadStores() async {
    try {
      final data = await widget.api.getList('/stores');
      setState(() => _stores = data.cast<Map<String, dynamic>>());
    } catch (_) {
      setState(() => _error = 'No se pudieron cargar las tiendas');
    } finally {
      setState(() => _loadingStores = false);
    }
  }

  Future<void> _submit() async {
    final priceText = _priceCtrl.text.trim();
    final price = double.tryParse(priceText);

    if (_selectedStore == null) {
      setState(() => _error = 'Selecciona una tienda');
      return;
    }
    if (price == null || price <= 0) {
      setState(() => _error = 'Ingresa un precio válido');
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      await widget.api.post('/prices', {
        'productId': widget.productId,
        'storeId': _selectedStore!['id'],
        'price': price,
        'currency': 'CRC',
      });
      if (mounted) Navigator.pop(context, true);
    } catch (_) {
      setState(() => _error = 'Error al enviar el precio. Intenta de nuevo.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  void dispose() {
    _priceCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 12),
          Center(
            child: Container(
              width: 36,
              height: 4,
              decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2)),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Reportar precio', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                Text(
                  widget.productName,
                  style: TextStyle(fontSize: 13, color: Colors.grey[600]),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
          const Divider(height: 16),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('¿En qué tienda estás?',
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
                const SizedBox(height: 8),
                _loadingStores
                    ? const Center(child: Padding(
                        padding: EdgeInsets.all(12),
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ))
                    : DropdownButtonFormField<Map<String, dynamic>>(
                        initialValue: _selectedStore,
                        hint: const Text('Seleccionar tienda'),
                        isExpanded: true,
                        decoration: InputDecoration(
                          border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                        ),
                        items: _stores.map((s) => DropdownMenuItem(
                              value: s,
                              child: Text(s['name'] as String),
                            )).toList(),
                        onChanged: (v) => setState(() {
                          _selectedStore = v;
                          _error = null;
                        }),
                      ),
                const SizedBox(height: 16),
                const Text('Precio en colones (₡)',
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
                const SizedBox(height: 8),
                TextField(
                  controller: _priceCtrl,
                  autofocus: true,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                  decoration: InputDecoration(
                    hintText: 'ej. 1250',
                    prefixText: '₡ ',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  onChanged: (_) => setState(() => _error = null),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 13)),
                ],
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    onPressed: _submitting ? null : _submit,
                    child: _submitting
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                          )
                        : const Text('Enviar precio'),
                  ),
                ),
                const SizedBox(height: 8),
                Center(
                  child: Text(
                    'Gracias por contribuir. Ganas puntos de reputación.',
                    style: TextStyle(fontSize: 11, color: Colors.grey[500]),
                  ),
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _EditProductNameSheet extends StatefulWidget {
  final ApiClient api;
  final String productId;
  final String currentName;

  const _EditProductNameSheet({
    required this.api,
    required this.productId,
    required this.currentName,
  });

  @override
  State<_EditProductNameSheet> createState() => _EditProductNameSheetState();
}

class _EditProductNameSheetState extends State<_EditProductNameSheet> {
  late final _nameCtrl = TextEditingController(
    text: widget.currentName == unnamedProductPlaceholder ? '' : widget.currentName,
  );
  bool _submitting = false;
  String? _error;

  Future<void> _submit() async {
    final name = _nameCtrl.text.trim();
    if (name.isEmpty) {
      setState(() => _error = 'Ingresa un nombre');
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      await widget.api.patch('/products/${widget.productId}', {'name': name});
      if (mounted) Navigator.pop(context, true);
    } catch (_) {
      setState(() => _error = 'Error al guardar el nombre. Intenta de nuevo.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 12),
          Center(
            child: Container(
              width: 36,
              height: 4,
              decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2)),
            ),
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 16, 16, 4),
            child: Text('Editar nombre del producto', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
          ),
          const Divider(height: 16),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Nombre del producto', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
                const SizedBox(height: 8),
                TextField(
                  controller: _nameCtrl,
                  autofocus: true,
                  textCapitalization: TextCapitalization.words,
                  decoration: InputDecoration(
                    hintText: 'ej. Galletas María Pozuelo',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  onChanged: (_) => setState(() => _error = null),
                  onSubmitted: (_) => _submit(),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 13)),
                ],
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    onPressed: _submitting ? null : _submit,
                    child: _submitting
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                          )
                        : const Text('Guardar'),
                  ),
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
